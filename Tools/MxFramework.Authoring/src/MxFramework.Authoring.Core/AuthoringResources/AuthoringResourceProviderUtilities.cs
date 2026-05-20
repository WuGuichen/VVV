using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    internal static class AuthoringResourceProviderUtilities
    {
        public static string BuildResourceId(string providerId, string stableId, string providerResourceKey)
        {
            return (providerId ?? string.Empty) + ":" + FirstNonEmpty(stableId, providerResourceKey);
        }

        public static void AddIfPresent(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            data[key] = value;
        }

        public static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        public static string SanitizeStableSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "resource";

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    builder.Append(c);
                else if (c == '.' || c == '_' || c == '-')
                    builder.Append(c);
                else if (c == '/' || c == '\\' || c == ':' || char.IsWhiteSpace(c))
                    builder.Append('.');
            }

            string result = builder.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "resource" : result;
        }

        public static string GetFileDisplayName(string path, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fileName = Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            return fallback ?? string.Empty;
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
            if (string.Equals(typeId, "TextAsset", StringComparison.Ordinal))
                return CharacterPackageResourceTypeIds.Config;

            return string.IsNullOrWhiteSpace(typeId) ? "unknown" : typeId;
        }
    }
}
