using System;
using System.IO;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Loads CharacterTest story content from built-in fixtures or StreamingAssets authoring JSON.
    /// </summary>
    public static class CharacterTestStoryContentLoader
    {
        public static CharacterTestStoryContent Load(
            bool useExternalStoryAuthoring,
            string streamingAssetsPath,
            IRuntimeLogger logger)
        {
            if (!useExternalStoryAuthoring)
            {
                logger?.Info("Story", "External Story authoring disabled. Using built-in fixture.");
                return CharacterTestStoryFixture.CreateBootstrapContent();
            }

            string absolutePath = ResolveStreamingAssetsPath(streamingAssetsPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                logger?.Warning(
                    "Story",
                    "Story draft not found. path=" + absolutePath + ". Using built-in fixture.");
                return CharacterTestStoryFixture.CreateBootstrapContent();
            }

            try
            {
                string json = File.ReadAllText(absolutePath);
                if (CharacterTestStoryDraftJson.TryLoad(
                    json,
                    out CharacterTestStoryContent content,
                    out string error,
                    sourcePath: absolutePath))
                {
                    logger?.Info(
                        "Story",
                        "External Story draft loaded. graphId=" + content.GraphId + ", path=" + absolutePath);
                    return content;
                }

                logger?.Warning("Story", error + " path=" + absolutePath + ". Using built-in fixture.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger?.Warning("Story", "Story draft read failed: " + ex.Message + ". Using built-in fixture.");
            }

            return CharacterTestStoryFixture.CreateBootstrapContent();
        }

        private static string ResolveStreamingAssetsPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            string normalizedPath = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.streamingAssetsPath, normalizedPath);
        }
    }
}
