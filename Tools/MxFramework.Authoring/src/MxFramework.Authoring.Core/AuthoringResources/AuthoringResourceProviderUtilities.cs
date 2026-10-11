using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    internal static class AuthoringResourceProviderUtilities
    {
        public static string BuildResourceId(string providerId, string stableId, string providerResourceKey)
        {
            return AuthoringResourceNaming.BuildProviderResourceId(providerId, stableId, providerResourceKey);
        }

        public static void AddIfPresent(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            data[key] = value;
        }

        public static string FirstNonEmpty(params string[] values)
        {
            return AuthoringResourceNaming.FirstNonEmpty(values);
        }

        public static string SanitizeStableSegment(string value)
        {
            return AuthoringResourceNaming.NormalizeStableSegment(value);
        }

        public static string GetFileDisplayName(string path, string fallback)
        {
            return AuthoringResourceNaming.GetFileDisplayName(path, fallback);
        }

        public static string ResolveProjectPath(string projectRootPath, string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
                return string.Empty;
            if (Path.IsPathRooted(projectRelativePath))
                return projectRelativePath;
            if (string.IsNullOrWhiteSpace(projectRootPath))
                return projectRelativePath;

            return Path.Combine(projectRootPath, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool ProjectFileExists(string projectRootPath, string projectRelativePath)
        {
            string fullPath = ResolveProjectPath(projectRootPath, projectRelativePath);
            return !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath);
        }

        public static string MapRuntimeTypeToLibraryKind(string typeId, string usage)
        {
            if (string.Equals(usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal) ||
                string.Equals(usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Model;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Animation;
            if (string.Equals(usage, "animationClip", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Animation;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.AudioCue, StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Audio;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.VfxCue, StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Vfx;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.PreviewThumbnail, StringComparison.Ordinal) ||
                string.Equals(usage, CharacterPackageResourceUsageIds.PreviewMesh, StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Preview;

            if (string.Equals(typeId, "GameObject", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Model;
            if (string.Equals(typeId, "Texture2D", StringComparison.Ordinal) ||
                string.Equals(typeId, "Sprite", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Texture;
            if (string.Equals(typeId, "Material", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Material;
            if (string.Equals(typeId, "AnimationClip", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Animation;
            if (string.Equals(typeId, "AudioClip", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Audio;
            if (string.Equals(typeId, "AvatarMask", StringComparison.Ordinal))
                return "avatarMask";
            if (string.Equals(typeId, "Scene", StringComparison.Ordinal) ||
                string.Equals(typeId, "SceneAsset", StringComparison.Ordinal))
                return "scene";
            if (string.Equals(typeId, "TextAsset", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Config;

            return string.IsNullOrWhiteSpace(typeId) ? "unknown" : typeId;
        }
    }
}
