using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    [InitializeOnLoad]
    internal static class MagicaCharacterClothOpenRequestBridge
    {
        private const string RequestRelativePath = "Temp/MxFrameworkAuthoring/CharacterCloth/open_request.json";
        private static double s_nextPoll;
        private static string s_lastRequestId = string.Empty;

        static MagicaCharacterClothOpenRequestBridge()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.delayCall += Consume;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_nextPoll) return;
            s_nextPoll = EditorApplication.timeSinceStartup + 1d;
            Consume();
        }

        private static void Consume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, RequestRelativePath);
            if (!File.Exists(path)) return;
            OpenRequest request;
            try { request = JsonConvert.DeserializeObject<OpenRequest>(File.ReadAllText(path)); }
            catch (Exception exception) when (exception is IOException || exception is JsonException) { Debug.LogWarning("MxFramework Character Cloth open request invalid: " + exception.Message); return; }
            if (request == null || string.IsNullOrWhiteSpace(request.requestId) || string.IsNullOrWhiteSpace(request.package)) return;
            try { File.Delete(path); } catch (IOException) { }
            if (string.Equals(s_lastRequestId, request.requestId, StringComparison.Ordinal)) return;
            s_lastRequestId = request.requestId;
            string packagePath = Path.GetFullPath(Path.Combine(projectRoot, request.package));
            MagicaCharacterClothAuthoringWindow.OpenPackage(packagePath);
        }

        [Serializable]
        private sealed class OpenRequest
        {
            public string requestId = string.Empty;
            public string package = string.Empty;
            public string packageId = string.Empty;
        }
    }
}
