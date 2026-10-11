using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.CharacterPreview
{
    /// <summary>A project-imported character identity, independent of an animation editor.</summary>
    public sealed class CharacterModelPreviewChoice
    {
        internal CharacterModelPreviewChoice(CharacterImportedPackage package, CharacterConfig character)
        {
            Package = package;
            Character = character;
            Key = package.PackageId + "/" + character.StableId;
            string displayName = character.StableId;
            string manifest = Path.Combine(package.RootPath, "package_cache/manifest.json");
            if (File.Exists(manifest))
            {
                try { displayName = (string)JObject.Parse(File.ReadAllText(manifest))["displayName"] ?? displayName; }
                catch (Newtonsoft.Json.JsonException) { }
            }
            Label = displayName + "  [" + package.PackageId + "]";
        }

        internal CharacterImportedPackage Package { get; }
        internal CharacterConfig Character { get; }
        public string Key { get; }
        public string Label { get; }
        public string DefaultLoadoutKey => Package.Configs.FindLoadout(Character.DefaultLoadoutId)?.StableId ?? string.Empty;
    }

    public readonly struct CharacterModelPreviewLoadout
    {
        internal CharacterModelPreviewLoadout(string key, string label) { Key = key; Label = label; }
        public string Key { get; }
        public string Label { get; }
    }

    public readonly struct CharacterModelPreviewClip
    {
        internal CharacterModelPreviewClip(string label, AnimationClip clip) { Label = label; Clip = clip; }
        public string Label { get; }
        public AnimationClip Clip { get; }
    }

    /// <summary>LowFreqAlloc: projects the existing imported character and animation catalogs for editor tools.</summary>
    public static class CharacterModelPreviewCatalog
    {
        public static CharacterModelPreviewChoice[] DiscoverCharacters()
        {
            var result = new List<CharacterModelPreviewChoice>();
            foreach (CharacterImportedPackage package in CharacterActionPreviewBindingDiscovery.LoadGeneratedPackages())
                foreach (CharacterConfig character in package.Configs.Characters)
                    if (character != null)
                        result.Add(new CharacterModelPreviewChoice(package, character));
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result.ToArray();
        }

        public static CharacterModelPreviewLoadout[] DiscoverLoadouts(CharacterModelPreviewChoice choice)
        {
            var result = new List<CharacterModelPreviewLoadout>();
            if (choice == null) return result.ToArray();
            var configs = choice.Package.Configs;
            foreach (EquipmentLoadoutConfig loadout in configs.Loadouts)
            {
                if (loadout == null || !loadout.EquipmentSchemaId.Equals(choice.Character.EquipmentSchemaId)) continue;
                var equipment = EquipmentStateResolver.Resolve(configs.FindEquipmentSchema(choice.Character.EquipmentSchemaId),
                    loadout, configs.Weapons, configs.EquipmentStates);
                var weapons = new List<string>();
                foreach (EquippedWeaponSlot slot in equipment.EquippedWeapons ?? Array.Empty<EquippedWeaponSlot>())
                {
                    int mountedSlot = slot.MountedSlotId > 0 ? slot.MountedSlotId : slot.SlotId;
                    string slotName = CharacterContentIdentityAuthoringCatalog.GetSourceKey(405, mountedSlot);
                    if (slotName == "mainHand") slotName = "主手";
                    else if (slotName == "offHand") slotName = "副手";
                    weapons.Add(slotName + ": " + (slot.Weapon?.StableId ?? "缺失武器"));
                }
                string prefix = "equip_loadout." + choice.Package.PackageId + ".";
                string name = loadout.StableId.StartsWith(prefix, StringComparison.Ordinal) ? loadout.StableId.Substring(prefix.Length) : loadout.StableId;
                string label = name + "  (" + (weapons.Count == 0 ? "空手" : string.Join(" / ", weapons)) + ")";
                if (!equipment.IsSuccess) label += " [配置无效]";
                result.Add(new CharacterModelPreviewLoadout(loadout.StableId, label));
            }
            return result.ToArray();
        }

        public static CharacterModelPreviewClip[] DiscoverClips(CharacterModelPreviewChoice choice)
        {
            var result = new List<CharacterModelPreviewClip>();
            var seen = new HashSet<AnimationClip>();
            foreach (var candidate in CharacterActionPreviewBindingDiscovery.DiscoverAnimationClips(choice?.Character.StableId))
                if (CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(candidate, out AnimationClip clip) && seen.Add(clip))
                    result.Add(new CharacterModelPreviewClip(candidate.Label, clip));
            return result.ToArray();
        }
    }
}
