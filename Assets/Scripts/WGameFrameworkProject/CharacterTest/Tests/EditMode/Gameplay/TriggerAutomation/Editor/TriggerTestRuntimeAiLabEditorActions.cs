using System;
using MxFramework.Game.CharacterTest;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestRuntimeAiLabEditorActions
    {
        internal const string TargetScenePath =
            "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity";

        internal static bool TryOpenTargetScene(
            out string message)
        {
            Scene active = SceneManager.GetActiveScene();
            bool requiresOpen =
                !string.Equals(
                    active.path,
                    TargetScenePath,
                    StringComparison.Ordinal) ||
                SceneManager.sceneCount != 1;
            if (requiresOpen && HasDirtyLoadedScene())
            {
                message =
                    "当前有未保存的场景修改；请先保存或撤销，再打开 Runtime AI Lab。";
                return false;
            }

            try
            {
                if (requiresOpen)
                {
                    EditorSceneManager.OpenScene(
                        TargetScenePath,
                        OpenSceneMode.Single);
                }
            }
            catch (Exception exception)
            {
                message =
                    "加载 Runtime AI Lab 场景失败：" +
                    exception.Message;
                return false;
            }

            if (!TryValidateTargetScene(out message))
                return false;

            message =
                "已打开 CharacterTestRuntimeAiLabScene，并验证唯一 Controller。";
            Debug.Log(
                "[TriggerTest][RuntimeAiLabOpenScene][PASS] " +
                message);
            return true;
        }

        internal static bool TryValidateTargetScene(
            out string message)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.Equals(
                    active.path,
                    TargetScenePath,
                    StringComparison.Ordinal))
            {
                message = "当前活动场景不是 Runtime AI Lab。";
                return false;
            }

            int liveCount = CountActiveControllers(
                out CharacterTestRuntimeAiPlannerDevSceneController _);
            if (liveCount != 1)
            {
                message = liveCount == 0
                    ? "Runtime AI Lab 场景缺少活动 Controller。"
                    : "Runtime AI Lab 场景存在多个活动 Controller。";
                return false;
            }

            message = "活动场景和唯一 Controller 校验通过。";
            return true;
        }

        internal static int CountActiveControllers(
            out CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestRuntimeAiPlannerDevSceneController[] controllers =
                Object.FindObjectsByType<
                    CharacterTestRuntimeAiPlannerDevSceneController>(
                    FindObjectsInactive.Exclude);
            controller = null;
            int liveCount = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterTestRuntimeAiPlannerDevSceneController candidate =
                    controllers[i];
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                controller = candidate;
                liveCount++;
            }

            if (liveCount != 1)
                controller = null;
            return liveCount;
        }

        private static bool HasDirtyLoadedScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                    return true;
            }

            return false;
        }
    }
}
