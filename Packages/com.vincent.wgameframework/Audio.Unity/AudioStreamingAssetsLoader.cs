using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MxFramework.Audio.Unity
{
    public readonly struct AudioTextLoadResult
    {
        public AudioTextLoadResult(bool success, string text, string source, string error)
        {
            Success = success;
            Text = text ?? string.Empty;
            Source = source ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Success { get; }
        public string Text { get; }
        public string Source { get; }
        public string Error { get; }
    }

    /// <summary>Unity boundary for StreamingAssets text on file and URL-backed platforms.</summary>
    public static class AudioStreamingAssetsLoader
    {
        public static bool RequiresWebRequest =>
            Application.platform == RuntimePlatform.Android
            || Application.platform == RuntimePlatform.WebGLPlayer
            || Application.streamingAssetsPath.Contains("://", StringComparison.Ordinal);

        /// <remarks>AllocByDesign startup path. Completes exactly once through the callback.</remarks>
        public static IEnumerator LoadText(string relativePath, Action<AudioTextLoadResult> completed)
        {
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));

            string normalized = Normalize(relativePath);
            if (string.IsNullOrEmpty(normalized))
            {
                completed(new AudioTextLoadResult(false, null, null, "StreamingAssets relative path is required."));
                yield break;
            }

            string source = CombineStreamingAssetsPath(normalized);
            if (!RequiresWebRequest)
            {
                try
                {
                    if (!File.Exists(source))
                    {
                        completed(new AudioTextLoadResult(false, null, source, "Audio startup artifact is missing: " + source));
                        yield break;
                    }
                    completed(new AudioTextLoadResult(true, File.ReadAllText(source), source, null));
                }
                catch (Exception exception)
                {
                    completed(new AudioTextLoadResult(false, null, source, "Audio startup artifact read failed: " + exception.Message));
                }
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(source))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed(new AudioTextLoadResult(
                        false,
                        null,
                        source,
                        "Audio startup artifact request failed: " + request.error));
                    yield break;
                }
                completed(new AudioTextLoadResult(true, request.downloadHandler.text, source, null));
            }
        }

        internal static string CombineStreamingAssetsPath(string relativePath)
        {
            string normalized = Normalize(relativePath);
            string root = Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/');
            return root + "/" + normalized;
        }

        internal static string Normalize(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            const string prefix = "Assets/StreamingAssets/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(prefix.Length);
            return normalized;
        }
    }
}
