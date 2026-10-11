using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor
{
    /// <summary>Stores artist-authored Element icons separately from rebuildable preview evidence.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Files are tracked authoring assets and never enter runtime VFX contracts.</remarks>
    internal static class VfxElementManualIconStore
    {
        internal const string Root = "Assets/VFX/MxFramework/Authoring/ElementIcons";

        internal static string GetIconPath(string elementId)
        {
            return GetElementDirectory(elementId) + "/icon.png";
        }

        internal static string GetMetadataPath(string elementId)
        {
            return GetElementDirectory(elementId) + "/icon.json";
        }

        internal static string ResolvePreferredIconPath(VfxSourceElementView element)
        {
            if (element == null || string.IsNullOrWhiteSpace(element.ElementId)) return string.Empty;

            string persistentIcon = GetIconPath(element.ElementId);
            if (File.Exists(persistentIcon)) return persistentIcon;

            string evidenceDirectory = string.IsNullOrWhiteSpace(element.PreviewEvidencePath)
                ? string.Empty
                : Path.GetDirectoryName(element.PreviewEvidencePath)?.Replace('\\', '/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(evidenceDirectory)) return string.Empty;

            string legacyIcon = evidenceDirectory + "/icon.png";
            if (File.Exists(legacyIcon) && TryMigrateLegacyIcon(element, legacyIcon, out string migratedPath))
                return migratedPath;

            string[] automaticCaptures = { "t025.png", "t010.png", "t050.png", "t075.png", "t100.png" };
            for (int i = 0; i < automaticCaptures.Length; i++)
            {
                string candidate = evidenceDirectory + "/" + automaticCaptures[i];
                if (File.Exists(candidate)) return candidate;
            }
            return string.Empty;
        }

        internal static bool Save(
            VfxSourceElementView element,
            byte[] pngBytes,
            JObject metadata,
            out string iconPath,
            out string message,
            Func<string, string, VfxElementManualIconPairTransaction> transactionFactory = null)
        {
            iconPath = string.Empty;
            if (element == null || string.IsNullOrWhiteSpace(element.ElementId))
            {
                message = "Element ID 为空，无法保存持久图标。";
                return false;
            }
            if (pngBytes == null || pngBytes.Length == 0)
            {
                message = "截图内容为空，无法保存持久图标。";
                return false;
            }

            try
            {
                iconPath = GetIconPath(element.ElementId);
                string metadataPath = GetMetadataPath(element.ElementId);

                JObject document = metadata == null ? new JObject() : (JObject)metadata.DeepClone();
                document["schemaVersion"] = "mx.vfx.element-icon.v2";
                document["elementId"] = element.ElementId;
                document["storagePolicy"] = "TrackedAuthoringAsset";
                document["iconPath"] = iconPath;
                document["contentHash"] = VfxCanonicalJson.HashUtf8(Convert.ToBase64String(pngBytes));
                using var transaction = transactionFactory != null
                    ? transactionFactory(iconPath, metadataPath)
                    : new VfxElementManualIconPairTransaction(iconPath, metadataPath);
                if (!transaction.Publish(pngBytes, document, out message)) return false;
                message = "已保存为项目持久 Element 图标。";
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is AggregateException)
            {
                message = "保存 Element 图标失败：" + exception.Message;
                return false;
            }
        }

        private static string GetElementDirectory(string elementId)
        {
            string hash = VfxCanonicalJson.HashUtf8(elementId ?? string.Empty);
            string token = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash.Substring(7, 24) : hash;
            return Root + "/" + token;
        }

        private static bool TryMigrateLegacyIcon(VfxSourceElementView element, string legacyIcon, out string migratedPath)
        {
            migratedPath = string.Empty;
            try
            {
                string legacyMetadataPath = Path.Combine(Path.GetDirectoryName(legacyIcon) ?? string.Empty, "icon.json");
                JObject metadata = File.Exists(legacyMetadataPath)
                    ? JsonConvert.DeserializeObject<JObject>(File.ReadAllText(legacyMetadataPath)) ?? new JObject()
                    : new JObject();
                metadata["migratedFrom"] = legacyIcon.Replace('\\', '/');
                return Save(element, File.ReadAllBytes(legacyIcon), metadata, out migratedPath, out _);
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

    }
}
