using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using MxFramework.Resources.Unity;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public static class CharacterRuntimePrefabResourceCatalog
    {
        public const string RuntimeResourcesAddressRoot = "MxFrameworkGenerated/CharacterPackages";

        public static string GetCharacterPrefabResourceId(string packageId)
        {
            return "char." + (packageId ?? string.Empty) + ".prefab.character_preview";
        }

        public static string GetWeaponPrefabResourceId(string packageId, string equipSlot, string weaponId)
        {
            return "char." + (packageId ?? string.Empty) + ".prefab.weapon."
                + SanitizeResourceIdSegment(equipSlot) + "." + SanitizeResourceIdSegment(weaponId);
        }

        public static string GetDefaultWeaponResourceId(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (HasRuntimeWeaponResource(attachment))
                return attachment.RuntimeResourceKey;

            return attachment == null
                ? string.Empty
                : GetWeaponPrefabResourceId(package?.PackageId, attachment.EquipmentSlotLabel, attachment.WeaponLabel);
        }

        public static string GetDefaultWeaponResourcePackageId(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return HasRuntimeWeaponResource(attachment)
                ? attachment.ResourcePackageId ?? string.Empty
                : package?.PackageId ?? string.Empty;
        }

        public static string GetDefaultWeaponResourceVariant(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return HasRuntimeWeaponResource(attachment) ? string.Empty : "default";
        }

        public static string GetWeaponVisualResourceKey(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (attachment == null)
                return string.Empty;

            return HasRuntimeWeaponResource(attachment)
                ? attachment.RuntimeResourceKey
                : attachment.PreviewResourceKey ?? string.Empty;
        }

        public static bool HasRuntimeWeaponResource(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return attachment != null && !string.IsNullOrWhiteSpace(attachment.RuntimeResourceKey);
        }

        public static string GetGeneratedCharacterPrefabPath(string importedPackageRoot, string packageId)
        {
            return NormalizeProjectPath(importedPackageRoot).TrimEnd('/')
                + "/prefabs/"
                + SanitizeName(packageId)
                + "_character_preview.prefab";
        }

        public static string GetGeneratedWeaponPrefabPath(
            string importedPackageRoot,
            string packageId,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (attachment == null)
                return string.Empty;

            return NormalizeProjectPath(importedPackageRoot).TrimEnd('/')
                + "/prefabs/weapons/"
                + SanitizeName(packageId)
                + "_"
                + SanitizeName(attachment.EquipmentSlotLabel)
                + "_"
                + SanitizeName(attachment.WeaponLabel)
                + ".prefab";
        }

        public static string GetRuntimeResourcesAddress(string importedPackageRoot, string packageId, string assetPath)
        {
            string relative = GetPackageRelativePath(importedPackageRoot, assetPath);
            string address = RuntimeResourcesAddressRoot + "/" + (packageId ?? string.Empty) + "/" + relative;
            return address.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? address.Substring(0, address.Length - ".prefab".Length)
                : address;
        }

        public static ResourceCatalogEntry CreateCharacterPrefabEntry(
            CharacterImportedPackage package,
            string importedPackageRoot,
            string characterPrefabPath,
            IEnumerable<string> labels = null)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            string root = NormalizeProjectPath(importedPackageRoot);
            string prefabPath = string.IsNullOrWhiteSpace(characterPrefabPath)
                ? GetGeneratedCharacterPrefabPath(root, package.PackageId)
                : NormalizeProjectPath(characterPrefabPath);

            return new ResourceCatalogEntry(
                GetCharacterPrefabResourceId(package.PackageId),
                ResourceTypeIds.GameObject,
                ResourcesProvider.Id,
                GetRuntimeResourcesAddress(root, package.PackageId, prefabPath),
                "default",
                package.PackageId,
                labels: labels ?? new[] { "CharacterSpawn" });
        }

        public static void AddWeaponPrefabEntries(
            CharacterImportedPackage package,
            string importedPackageRoot,
            IList<ResourceCatalogEntry> entries,
            ISet<string> knownResourceIds = null,
            IEnumerable<string> labels = null,
            bool existingFilesOnly = true)
        {
            if (package == null || entries == null)
                return;

            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterWeaponAttachmentRuntimeBinding[] attachments = package.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                    continue;

                string weaponPrefabPath = GetGeneratedWeaponPrefabPath(root, package.PackageId, attachment);
                if (existingFilesOnly && !File.Exists(weaponPrefabPath))
                    continue;

                string weaponResourceId = GetWeaponPrefabResourceId(
                    package.PackageId,
                    attachment.EquipmentSlotLabel,
                    attachment.WeaponLabel);
                if (knownResourceIds != null && !knownResourceIds.Add(weaponResourceId))
                    continue;

                entries.Add(new ResourceCatalogEntry(
                    weaponResourceId,
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    GetRuntimeResourcesAddress(root, package.PackageId, weaponPrefabPath),
                    "default",
                    package.PackageId,
                    labels: labels ?? new[] { "CharacterEquipment" }));
            }
        }

        private static string GetPackageRelativePath(string importedPackageRoot, string assetPath)
        {
            string root = NormalizeProjectPath(importedPackageRoot).TrimEnd('/');
            string normalized = NormalizeProjectPath(assetPath);
            string prefix = root + "/";
            return normalized.StartsWith(prefix, StringComparison.Ordinal)
                ? normalized.Substring(prefix.Length)
                : Path.GetFileName(normalized);
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('.', '_').Replace('/', '_');
        }

        private static string SanitizeResourceIdSegment(string value)
        {
            return SanitizeName(value).ToLowerInvariant();
        }
    }
}
