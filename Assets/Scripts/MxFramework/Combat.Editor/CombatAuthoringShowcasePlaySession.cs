using System;
using System.Text;
using MxFramework.Combat.Authoring;
using MxFramework.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Combat.Editor
{
    [InitializeOnLoad]
    internal static class CombatAuthoringShowcasePlaySession
    {
        private const string TargetScenePath = "Assets/Scenes/CombatAnimationPhysicsTest.unity";
        private const string ActionGuidKey = "MxFramework.Combat.Authoring.Showcase.ActionGuid";
        private const string BindingGuidKey = "MxFramework.Combat.Authoring.Showcase.BindingGuid";
        private const string RequestedAtKey = "MxFramework.Combat.Authoring.Showcase.RequestedAt";
        private const string ValidationSummaryKey = "MxFramework.Combat.Authoring.Showcase.ValidationSummary";
        private const string LastStatusKey = "MxFramework.Combat.Authoring.Showcase.LastStatus";
        private const string DefaultPlayerMarkerName = "Combat_Player_Marker";
        private const string DefaultEnemyMarkerName = "Combat_Enemy_Marker";

        static CombatAuthoringShowcasePlaySession()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static string LastStatus => SessionState.GetString(LastStatusKey, string.Empty);

        public static bool HasSession => !string.IsNullOrEmpty(SessionState.GetString(ActionGuidKey, string.Empty));

        public static bool Begin(
            CombatActionAuthoringAsset action,
            CombatSceneBindingAsset binding,
            CombatAuthoringReport report,
            out string status)
        {
            string actionGuid = AssetGuid(action);
            if (string.IsNullOrEmpty(actionGuid))
            {
                status = "Showcase 预览失败：请先保存 Action Asset，未保存资产没有稳定 GUID。";
                SetLastStatus(status);
                return false;
            }

            SessionState.SetString(ActionGuidKey, actionGuid);
            SessionState.SetString(BindingGuidKey, AssetGuid(binding));
            SessionState.SetString(RequestedAtKey, DateTime.UtcNow.ToString("O"));
            SessionState.SetString(ValidationSummaryKey, BuildValidationSummary(report));

            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += ApplyPendingSessionInPlayMode;
                status = "已更新 Play Mode Showcase 预览会话。";
                SetLastStatus(status);
                return true;
            }

            if (!OpenTargetSceneIfNeeded(out status))
            {
                SetLastStatus(status);
                return false;
            }

            EditorApplication.EnterPlaymode();
            status = "已启动 Showcase 预览，会在进入 Play Mode 后应用当前 Action / Binding。";
            SetLastStatus(status);
            return true;
        }

        public static void Clear()
        {
            SessionState.EraseString(ActionGuidKey);
            SessionState.EraseString(BindingGuidKey);
            SessionState.EraseString(RequestedAtKey);
            SessionState.EraseString(ValidationSummaryKey);
            SetLastStatus("已清除 Showcase 预览会话。");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                ApplyPendingSessionInPlayMode();
            else if (state == PlayModeStateChange.ExitingPlayMode)
                SetLastStatus("已退出 Showcase Play Mode，预览会话仍可手动清除或覆盖。");
        }

        private static void ApplyPendingSessionInPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return;

            string actionGuid = SessionState.GetString(ActionGuidKey, string.Empty);
            if (string.IsNullOrEmpty(actionGuid))
                return;

            CombatActionAuthoringAsset action = LoadAsset<CombatActionAuthoringAsset>(actionGuid);
            CombatSceneBindingAsset binding = LoadAsset<CombatSceneBindingAsset>(SessionState.GetString(BindingGuidKey, string.Empty));
            if (action == null)
            {
                SetLastStatus("Showcase 预览失败：找不到会话记录的 Action Asset。");
                return;
            }

            RuntimeCombatShowcaseRunner runner = FindOrCreateRunner();
            RuntimeCombatShowcaseAuthoringConfig config = BuildRuntimeConfig(action, binding);
            runner.ApplyAuthoringPreviewConfig(config);
            EnsureShowcaseUi(runner);
            SetLastStatus("Showcase 预览已应用：" + config.SourceSummary);
            ClearPendingSessionData();
        }

        private static RuntimeCombatShowcaseAuthoringConfig BuildRuntimeConfig(
            CombatActionAuthoringAsset action,
            CombatSceneBindingAsset binding)
        {
            var markerSummary = new StringBuilder();
            Transform playerMarker = ResolveActorMarker(binding, DefaultPlayerMarkerName, 1, "Player", markerSummary);
            Transform enemyMarker = ResolveActorMarker(binding, DefaultEnemyMarkerName, 2, "Enemy", markerSummary);
            string bindingName = binding == null ? "未选择 Binding" : binding.name;
            string sourceSummary = "Authoring Preview: " + action.name + " / " + bindingName;
            int traceId = FirstTraceId(action);

            return new RuntimeCombatShowcaseAuthoringConfig(
                sourceSummary,
                action.ActionId,
                traceId,
                playerMarker,
                enemyMarker,
                SessionState.GetString(ValidationSummaryKey, "validation: 未执行"),
                markerSummary.ToString().Trim());
        }

        private static RuntimeCombatShowcaseRunner FindOrCreateRunner()
        {
            RuntimeCombatShowcaseRunner runner = UnityEngine.Object.FindFirstObjectByType<RuntimeCombatShowcaseRunner>();
            if (runner != null)
                return runner;

            var gameObject = new GameObject("RuntimeCombatShowcase_AuthoringPreview");
            gameObject.hideFlags = HideFlags.DontSaveInEditor;
            runner = gameObject.AddComponent<RuntimeCombatShowcaseRunner>();
            gameObject.AddComponent<RuntimeCombatShowcaseUi>();
            return runner;
        }

        private static void EnsureShowcaseUi(RuntimeCombatShowcaseRunner runner)
        {
            if (runner != null && runner.GetComponent<RuntimeCombatShowcaseUi>() == null)
                runner.gameObject.AddComponent<RuntimeCombatShowcaseUi>();
        }

        private static Transform ResolveActorMarker(
            CombatSceneBindingAsset binding,
            string fallbackName,
            int entityId,
            string displayNameToken,
            StringBuilder markerSummary)
        {
            string markerId = FindActorMarkerId(binding, entityId, displayNameToken);
            Transform marker = ResolveMarkerTarget(binding, markerId);
            if (marker != null)
            {
                markerSummary.AppendLine(displayNameToken + " marker: " + marker.name + " (" + markerId + ")");
                return marker;
            }

            Transform fallback = FindSceneTransform(fallbackName);
            if (fallback != null)
            {
                markerSummary.AppendLine(displayNameToken + " marker fallback: " + fallbackName + "（Binding targetPath 未解析）");
                return fallback;
            }

            markerSummary.AppendLine(displayNameToken + " marker 错误：Binding targetPath 未解析，且找不到 fallback " + fallbackName);
            return null;
        }

        private static string FindActorMarkerId(CombatSceneBindingAsset binding, int entityId, string displayNameToken)
        {
            if (binding == null || binding.Actors == null)
                return string.Empty;

            CombatActorBindingData[] actors = binding.Actors;
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i].EntityId == entityId)
                    return actors[i].MarkerId;
            }

            for (int i = 0; i < actors.Length; i++)
            {
                if (!string.IsNullOrEmpty(actors[i].DisplayName)
                    && actors[i].DisplayName.IndexOf(displayNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    return actors[i].MarkerId;
            }

            return string.Empty;
        }

        private static Transform ResolveMarkerTarget(CombatSceneBindingAsset binding, string markerId)
        {
            if (binding == null || string.IsNullOrEmpty(markerId) || binding.Markers == null)
                return null;

            CombatMarkerBindingData[] markers = binding.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (!string.Equals(markers[i].MarkerId, markerId, StringComparison.Ordinal))
                    continue;

                return FindSceneTransform(markers[i].TargetPath);
            }

            return null;
        }

        private static Transform FindSceneTransform(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
                return null;

            GameObject target = GameObject.Find(targetPath);
            if (target != null)
                return target.transform;

            int slash = targetPath.LastIndexOf('/');
            string name = slash >= 0 ? targetPath.Substring(slash + 1) : targetPath;
            target = GameObject.Find(name);
            return target == null ? null : target.transform;
        }

        private static int FirstTraceId(CombatActionAuthoringAsset action)
        {
            if (action == null || action.WeaponTraces == null || action.WeaponTraces.Length == 0)
                return 0;

            int traceId = 0;
            int bestOrder = int.MaxValue;
            CombatWeaponTraceAuthoringData[] traces = action.WeaponTraces;
            for (int i = 0; i < traces.Length; i++)
            {
                if (traces[i].TraceId <= 0)
                    continue;

                if (traces[i].SourceOrder < bestOrder)
                {
                    traceId = traces[i].TraceId;
                    bestOrder = traces[i].SourceOrder;
                }
            }

            return traceId;
        }

        private static bool OpenTargetSceneIfNeeded(out string status)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.Equals(active.path, TargetScenePath, StringComparison.Ordinal))
            {
                status = string.Empty;
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                status = "Showcase 预览已取消：当前场景有未保存改动。";
                return false;
            }

            EditorSceneManager.OpenScene(TargetScenePath);
            status = string.Empty;
            return true;
        }

        private static string BuildValidationSummary(CombatAuthoringReport report)
        {
            if (report == null || report.IssueCount == 0)
                return "validation: 通过";

            int warnings = 0;
            int errors = 0;
            for (int i = 0; i < report.IssueCount; i++)
            {
                if (report.GetIssue(i).Severity == CombatAuthoringSeverity.Error)
                    errors++;
                else if (report.GetIssue(i).Severity == CombatAuthoringSeverity.Warning)
                    warnings++;
            }

            return errors > 0
                ? "validation: 有 error=" + errors + " warning=" + warnings
                : "validation: 通过，有 warning=" + warnings;
        }

        private static T LoadAsset<T>(string guid)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static string AssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static void SetLastStatus(string status)
        {
            SessionState.SetString(LastStatusKey, status ?? string.Empty);
        }

        private static void ClearPendingSessionData()
        {
            SessionState.EraseString(ActionGuidKey);
            SessionState.EraseString(BindingGuidKey);
            SessionState.EraseString(RequestedAtKey);
            SessionState.EraseString(ValidationSummaryKey);
        }
    }
}
