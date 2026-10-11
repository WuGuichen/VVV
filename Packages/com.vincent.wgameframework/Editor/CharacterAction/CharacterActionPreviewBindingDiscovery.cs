using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionPreviewBindingDiscovery
    {
        public const string GeneratedPackagesRoot = "Assets/MxFrameworkGenerated/CharacterPackages";
        [ThreadStatic] private static int _readScopeDepth;
        [ThreadStatic] private static CharacterImportedPackage[] _readScopePackages;

        internal static IDisposable BeginReadScope() => new ReadScope();

        private sealed class ReadScope : IDisposable
        {
            internal ReadScope() { _readScopeDepth++; }
            public void Dispose()
            {
                if (--_readScopeDepth == 0)
                    _readScopePackages = null;
            }
        }

        public static CharacterActionPreviewBindingChoice[] DiscoverCharacters()
        {
            CharacterImportedPackage[] packages = LoadGeneratedPackages();
            var choices = new List<CharacterActionPreviewBindingChoice>
            {
                new CharacterActionPreviewBindingChoice(string.Empty, "No preview character"),
            };
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < packages.Length; i++)
            {
                CharacterImportedPackage package = packages[i];
                CharacterConfig[] characters = package.Configs?.Characters ?? Array.Empty<CharacterConfig>();
                for (int j = 0; j < characters.Length; j++)
                {
                    CharacterConfig character = characters[j];
                    if (character == null || string.IsNullOrWhiteSpace(character.StableId))
                        continue;
                    if (!emitted.Add(character.StableId))
                        continue;

                    choices.Add(new CharacterActionPreviewBindingChoice(
                        character.StableId,
                        character.StableId + "  [" + package.PackageId + "]"));
                }
            }

            return choices.ToArray();
        }

        public static CharacterActionPreviewBindingChoice[] DiscoverEquipmentStates(string characterStableId)
        {
            var choices = new List<CharacterActionPreviewBindingChoice>
            {
                new CharacterActionPreviewBindingChoice(string.Empty, "Default loadout"),
            };

            if (!TryFindCharacter(characterStableId, out CharacterImportedPackage package, out CharacterConfig character))
                return choices.ToArray();

            CharacterImportedConfigSet configs = package.Configs;
            EquipmentSchemaConfig schema = configs.FindEquipmentSchema(character.EquipmentSchemaId);
            if (schema == null)
                return choices.ToArray();

            var emitted = new HashSet<string>(StringComparer.Ordinal);
            EquipmentLoadoutConfig[] loadouts = configs.Loadouts ?? Array.Empty<EquipmentLoadoutConfig>();
            for (int i = 0; i < loadouts.Length; i++)
            {
                EquipmentLoadoutConfig loadout = loadouts[i];
                if (loadout == null || !loadout.EquipmentSchemaId.Equals(character.EquipmentSchemaId))
                    continue;

                EquipmentStateResolveResult result = EquipmentStateResolver.Resolve(
                    schema,
                    loadout,
                    configs.Weapons,
                    configs.EquipmentStates);
                EquipmentStateConfig state = result.ActiveState;
                if (!result.IsSuccess || state == null || string.IsNullOrWhiteSpace(state.StableId))
                    continue;
                if (!emitted.Add(state.StableId))
                    continue;

                string label = state.StableId + "  [" + loadout.StableId + "]";
                string weapons = FormatWeapons(result.EquippedWeapons);
                if (!string.IsNullOrWhiteSpace(weapons))
                    label += "  " + weapons;
                choices.Add(new CharacterActionPreviewBindingChoice(state.StableId, label));
            }

            return choices.ToArray();
        }

        internal static bool TryGetDefaultEquipmentStateStableId(string characterStableId, out string stableId)
        {
            stableId = string.Empty;
            if (!TryFindCharacter(characterStableId, out CharacterImportedPackage package, out CharacterConfig character)
                || !TryResolveEquipmentContext(package.Configs, character, string.Empty,
                    out EquipmentStateConfig activeState, out _)
                || string.IsNullOrWhiteSpace(activeState?.StableId))
            {
                return false;
            }

            stableId = activeState.StableId;
            return true;
        }

        public static int[] DiscoverKnownAbilityIds(string characterStableId, string equipmentStateStableId)
        {
            if (!TryFindCharacter(characterStableId, out CharacterImportedPackage package, out CharacterConfig character))
                return Array.Empty<int>();

            CharacterImportedConfigSet configs = package.Configs;
            if (!TryResolveEquipmentContext(configs, character, equipmentStateStableId, out EquipmentStateConfig activeState, out EquippedWeaponSlot[] equippedWeapons))
                return Array.Empty<int>();

            CharacterAbilityId[] knownAbilityIds = BuildKnownAbilityIds(configs.AbilityLoadouts);
            AbilityGrantResolveResult result = AbilityGrantResolver.Resolve(new AbilityGrantResolveRequest(
                character,
                equippedWeapons,
                activeState,
                configs.AbilityLoadouts,
                knownAbilityIds: knownAbilityIds));

            var ids = new List<int>();
            CharacterAbilityId[] effective = result.EffectiveAbilityIds ?? Array.Empty<CharacterAbilityId>();
            for (int i = 0; i < effective.Length; i++)
            {
                int value = effective[i].Value;
                if (value > 0 && !ids.Contains(value))
                    ids.Add(value);
            }

            ids.Sort();
            return ids.ToArray();
        }

        public static CharacterActionAnimationClipChoice[] DiscoverAnimationClips(string characterStableId)
        {
            CharacterImportedPackage[] packages = LoadGeneratedPackages();
            var result = new List<CharacterActionAnimationClipChoice>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < packages.Length; i++)
            {
                CharacterImportedPackage package = packages[i];
                if (!string.IsNullOrWhiteSpace(characterStableId)
                    && !PackageContainsCharacter(package, characterStableId))
                {
                    continue;
                }

                if (!CharacterRuntimeAnimationResources.TryCreateBinding(
                        package,
                        out CharacterRuntimeAnimationBinding binding,
                        out _))
                {
                    continue;
                }
                IEnumerable<ResourceCatalogEntry> entries = CharacterRuntimeAnimationResources.CreateCatalogEntries(
                    binding,
                    "character-action-editor-discovery",
                    useRuntimeResourceKeyAsAddress: true);
                foreach (ResourceCatalogEntry entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !emitted.Add(entry.Id))
                        continue;

                    AddAnimationClipChoice(result, entry, package.PackageId);
                }
            }

            ResourceCatalogEntry[] runtimeEntries =
                CharacterActionRuntimeAnimationResourceDiscovery.DiscoverRuntimeAnimationClipEntries(
                    "character-action-editor-discovery");
            for (int i = 0; i < runtimeEntries.Length; i++)
            {
                ResourceCatalogEntry entry = runtimeEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !emitted.Add(entry.Id))
                    continue;

                AddAnimationClipChoice(result, entry, FormatResourceManagerOwner(entry.PackageId));
            }

            return result.ToArray();
        }

        public static bool TryFindAnimationClipChoice(
            string resourceKey,
            string characterStableId,
            out CharacterActionAnimationClipChoice choice)
        {
            choice = default;
            if (string.IsNullOrWhiteSpace(resourceKey))
                return false;

            CharacterActionAnimationClipChoice[] choices = DiscoverAnimationClips(characterStableId);
            for (int i = 0; i < choices.Length; i++)
            {
                if (!string.Equals(choices[i].ResourceKey, resourceKey, StringComparison.Ordinal))
                    continue;
                choice = choices[i];
                return true;
            }

            return false;
        }

        public static bool TryFindAnimationClipChoice(
            AnimationClip clip,
            string characterStableId,
            out CharacterActionAnimationClipChoice choice)
        {
            choice = default;
            if (clip == null)
                return false;

            string assetPath = NormalizeProjectPath(AssetDatabase.GetAssetPath(clip));
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string clipName = clip.name ?? string.Empty;
            CharacterActionAnimationClipChoice[] choices = DiscoverAnimationClips(characterStableId);
            for (int i = 0; i < choices.Length; i++)
            {
                CharacterActionAnimationClipChoice candidate = choices[i];
                if (!string.Equals(NormalizeProjectPath(candidate.UnityAssetPath), assetPath, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrWhiteSpace(candidate.SourceClipName)
                    || string.Equals(candidate.SourceClipName, clipName, StringComparison.Ordinal))
                {
                    choice = candidate;
                    return true;
                }

                if (TryLoadAnimationClip(candidate, out AnimationClip loaded) && loaded == clip)
                {
                    choice = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryLoadAnimationClip(CharacterActionAnimationClipChoice choice, out AnimationClip clip)
        {
            clip = null;
            string assetPath = NormalizeProjectPath(choice.UnityAssetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            AnimationClip direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (direct != null
                && (string.IsNullOrWhiteSpace(choice.SourceClipName)
                    || string.Equals(direct.name, choice.SourceClipName, StringComparison.Ordinal)))
            {
                clip = direct;
                return true;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip first = direct;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip candidate))
                    continue;

                first ??= candidate;
                if (string.Equals(candidate.name, choice.SourceClipName, StringComparison.Ordinal))
                {
                    clip = candidate;
                    return true;
                }
            }

            clip = first;
            return clip != null;
        }

        public static void AddGeneratedReferences(CharacterActionAuthoringReferenceContext context)
        {
            if (context == null)
                return;

            CharacterImportedPackage[] packages = LoadGeneratedPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                CharacterImportedConfigSet configs = packages[i].Configs;
                CharacterConfig[] characters = configs?.Characters ?? Array.Empty<CharacterConfig>();
                for (int j = 0; j < characters.Length; j++)
                    context.AddCharacterStableId(characters[j]?.StableId);

                EquipmentStateConfig[] states = configs?.EquipmentStates ?? Array.Empty<EquipmentStateConfig>();
                for (int j = 0; j < states.Length; j++)
                    context.AddEquipmentStateStableId(states[j]?.StableId);
            }

            CharacterActionAnimationClipChoice[] clips = DiscoverAnimationClips(string.Empty);
            for (int i = 0; i < clips.Length; i++)
                context.AddAnimationActionKey(clips[i].ResourceKey);
        }

        private static bool TryFindCharacter(
            string characterStableId,
            out CharacterImportedPackage package,
            out CharacterConfig character)
        {
            package = null;
            character = null;
            if (string.IsNullOrWhiteSpace(characterStableId))
                return false;

            CharacterImportedPackage[] packages = LoadGeneratedPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                CharacterConfig[] characters = packages[i].Configs?.Characters ?? Array.Empty<CharacterConfig>();
                for (int j = 0; j < characters.Length; j++)
                {
                    if (!string.Equals(characters[j]?.StableId, characterStableId, StringComparison.Ordinal))
                        continue;
                    package = packages[i];
                    character = characters[j];
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveEquipmentContext(
            CharacterImportedConfigSet configs,
            CharacterConfig character,
            string equipmentStateStableId,
            out EquipmentStateConfig activeState,
            out EquippedWeaponSlot[] equippedWeapons)
        {
            activeState = null;
            equippedWeapons = Array.Empty<EquippedWeaponSlot>();
            if (configs == null || character == null)
                return false;

            EquipmentSchemaConfig schema = configs.FindEquipmentSchema(character.EquipmentSchemaId);
            if (schema == null)
                return false;

            if (!string.IsNullOrWhiteSpace(equipmentStateStableId))
            {
                EquipmentLoadoutConfig[] loadouts = configs.Loadouts ?? Array.Empty<EquipmentLoadoutConfig>();
                for (int i = 0; i < loadouts.Length; i++)
                {
                    EquipmentLoadoutConfig loadout = loadouts[i];
                    if (loadout == null || !loadout.EquipmentSchemaId.Equals(character.EquipmentSchemaId))
                        continue;

                    EquipmentStateResolveResult result = EquipmentStateResolver.Resolve(
                        schema,
                        loadout,
                        configs.Weapons,
                        configs.EquipmentStates);
                    if (!result.IsSuccess || result.ActiveState == null)
                        continue;
                    if (!string.Equals(result.ActiveState.StableId, equipmentStateStableId, StringComparison.Ordinal))
                        continue;

                    activeState = result.ActiveState;
                    equippedWeapons = result.EquippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
                    return true;
                }
            }

            EquipmentLoadoutConfig defaultLoadout = configs.FindLoadout(character.DefaultLoadoutId);
            if (defaultLoadout == null)
                return false;

            EquipmentStateResolveResult fallback = EquipmentStateResolver.Resolve(
                schema,
                defaultLoadout,
                configs.Weapons,
                configs.EquipmentStates);
            activeState = fallback.ActiveState;
            equippedWeapons = fallback.EquippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
            return fallback.IsSuccess;
        }

        private static CharacterAbilityId[] BuildKnownAbilityIds(AbilityLoadoutConfig[] abilityLoadouts)
        {
            abilityLoadouts = abilityLoadouts ?? Array.Empty<AbilityLoadoutConfig>();
            var ids = new List<CharacterAbilityId>();
            for (int i = 0; i < abilityLoadouts.Length; i++)
            {
                CharacterAbilityId[] abilityIds = abilityLoadouts[i]?.AbilityIds ?? Array.Empty<CharacterAbilityId>();
                for (int j = 0; j < abilityIds.Length; j++)
                {
                    CharacterAbilityId abilityId = abilityIds[j];
                    if (abilityId.IsValid && !ContainsAbilityId(ids, abilityId))
                        ids.Add(abilityId);
                }
            }

            return ids.ToArray();
        }

        private static bool ContainsAbilityId(List<CharacterAbilityId> ids, CharacterAbilityId abilityId)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i].Equals(abilityId))
                    return true;
            }

            return false;
        }

        private static bool PackageContainsCharacter(CharacterImportedPackage package, string characterStableId)
        {
            CharacterConfig[] characters = package?.Configs?.Characters ?? Array.Empty<CharacterConfig>();
            for (int i = 0; i < characters.Length; i++)
            {
                if (string.Equals(characters[i]?.StableId, characterStableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal static CharacterImportedPackage[] LoadGeneratedPackages()
        {
            if (_readScopeDepth > 0 && _readScopePackages != null)
                return _readScopePackages;
            if (!Directory.Exists(GeneratedPackagesRoot))
                return Array.Empty<CharacterImportedPackage>();

            var packages = new List<CharacterImportedPackage>();
            string[] directories = Directory.GetDirectories(GeneratedPackagesRoot);
            for (int i = 0; i < directories.Length; i++)
            {
                try
                {
                    packages.Add(CharacterImportedPackageJson.LoadFromDirectory(NormalizeProjectPath(directories[i])));
                }
                catch
                {
                    // Ignore partial package folders. The preview session reports concrete binding errors.
                }
            }

            CharacterImportedPackage[] result = packages.ToArray();
            if (_readScopeDepth > 0)
                _readScopePackages = result;
            return result;
        }

        private static string FormatWeapons(EquippedWeaponSlot[] weapons)
        {
            weapons = weapons ?? Array.Empty<EquippedWeaponSlot>();
            if (weapons.Length == 0)
                return string.Empty;

            var labels = new List<string>(weapons.Length);
            for (int i = 0; i < weapons.Length; i++)
            {
                EquippedWeaponSlot weapon = weapons[i];
                if (weapon.Weapon == null)
                    continue;
                int slotId = weapon.MountedSlotId <= 0 ? weapon.SlotId : weapon.MountedSlotId;
                string slot = CharacterContentIdentityAuthoringCatalog.GetSourceKey(405, slotId);
                labels.Add(slot + ":" + weapon.Weapon.StableId);
            }

            return labels.Count == 0 ? string.Empty : "(" + string.Join(", ", labels) + ")";
        }

        private static string BuildAnimationClipLabel(
            string resourceKey,
            string clipId,
            string sourceClipName,
            string unityAssetPath,
            string packageId)
        {
            string primary = string.IsNullOrWhiteSpace(sourceClipName) ? resourceKey : sourceClipName;
            string id = string.IsNullOrWhiteSpace(clipId) ? resourceKey : clipId;
            string owner = string.IsNullOrWhiteSpace(packageId) ? "Resource Manager" : packageId;
            string label = primary + "  [" + id + " @ " + owner + "]";
            if (!string.IsNullOrWhiteSpace(unityAssetPath))
                label += "  " + unityAssetPath;
            return label;
        }

        private static void AddAnimationClipChoice(
            List<CharacterActionAnimationClipChoice> result,
            ResourceCatalogEntry entry,
            string packageId)
        {
            CharacterRuntimeAnimationResources.TryGetCatalogEntryClipId(entry, out string clipId);
            CharacterRuntimeAnimationResources.TryGetCatalogEntrySourceClipName(entry, out string sourceClipName);
            CharacterRuntimeAnimationResources.TryGetCatalogEntryUnityAssetPath(entry, out string unityAssetPath);
            result.Add(new CharacterActionAnimationClipChoice(
                entry.Id,
                BuildAnimationClipLabel(entry.Id, clipId, sourceClipName, unityAssetPath, packageId),
                packageId,
                clipId,
                sourceClipName,
                unityAssetPath));
        }

        private static string FormatResourceManagerOwner(string packageId)
        {
            return string.IsNullOrWhiteSpace(packageId)
                ? "Resource Manager"
                : "Resource Manager: " + packageId;
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }

    internal readonly struct CharacterActionPreviewBindingChoice
    {
        public CharacterActionPreviewBindingChoice(string value, string label)
        {
            Value = value ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Value { get; }
        public string Label { get; }
    }

    internal readonly struct CharacterActionAnimationClipChoice
    {
        public CharacterActionAnimationClipChoice(
            string resourceKey,
            string label,
            string packageId,
            string clipId,
            string sourceClipName,
            string unityAssetPath)
        {
            ResourceKey = resourceKey ?? string.Empty;
            Label = label ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            ClipId = clipId ?? string.Empty;
            SourceClipName = sourceClipName ?? string.Empty;
            UnityAssetPath = unityAssetPath ?? string.Empty;
        }

        public string ResourceKey { get; }
        public string Label { get; }
        public string PackageId { get; }
        public string ClipId { get; }
        public string SourceClipName { get; }
        public string UnityAssetPath { get; }
    }
}
