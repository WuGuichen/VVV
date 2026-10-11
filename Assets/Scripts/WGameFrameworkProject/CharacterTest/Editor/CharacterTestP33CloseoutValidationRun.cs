using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Core.Math;
using MxFramework.Editor.Levels;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestP33CloseoutValidationRun
    {
        private const string MenuPath =
            "MxFramework/CharacterTest/Validation/Run P3.3 Closeout";
        private const string ReportDirectory =
            "Temp/P33Closeout";
        private const string EditModeReportPath =
            ReportDirectory + "/editmode.txt";
        private const string PlayModeReportPath =
            ReportDirectory + "/playmode.txt";
        private const string VisualReportPath =
            ReportDirectory + "/visual.txt";
        private const string StandardCapturePath =
            ReportDirectory + "/workspace-standard.png";
        private const string NarrowCapturePath =
            ReportDirectory + "/workspace-narrow.png";
        private const string PlayerHpCapturePath =
            ReportDirectory + "/workspace-player-hp.png";

        private static CharacterTestP33ValidationWindow s_window;
        private static CharacterTestPerformanceUnityTestReportRun s_editRun;
        private static int s_phase;
        private static int s_frames;
        private static bool s_reopenLevelEditorAfterTests;

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (s_window != null
                || (s_editRun != null && !s_editRun.IsDone)
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "P3.3 closeout validation is already running or Unity "
                    + "is changing Play Mode state.");
                return;
            }

            Directory.CreateDirectory(ReportDirectory);
            s_phase = 0;
            s_frames = 0;
            s_reopenLevelEditorAfterTests = false;
            s_editRun = null;
            Debug.Log("P3.3 closeout validation started manually.");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            try
            {
                switch (s_phase)
                {
                    case 0:
                        BeginVisualValidation();
                        break;
                    case 1:
                        if (++s_frames >= 12)
                        {
                            CaptureWindow(s_window, StandardCapturePath);
                            s_window.ApplyNarrowWidth();
                            s_frames = 0;
                            s_phase = 2;
                        }
                        break;
                    case 2:
                        if (++s_frames >= 12)
                        {
                            CaptureWindow(s_window, NarrowCapturePath);
                            s_window.ApplyPlayerHpStandard();
                            s_frames = 0;
                            s_phase = 3;
                        }
                        break;
                    case 3:
                        if (++s_frames >= 12)
                        {
                            CaptureWindow(s_window, PlayerHpCapturePath);
                            WriteVisualReport();
                            s_window.Close();
                            s_window = null;
                            StartEditModeTests();
                            s_phase = 4;
                        }
                        break;
                    case 4:
                        if (s_editRun?.IsDone == true)
                        {
                            if (s_reopenLevelEditorAfterTests)
                            {
                                MxLevelEditorWindow.OpenWindow();
                                s_reopenLevelEditorAfterTests = false;
                            }
                            StartPlayModeTests();
                            EditorApplication.update -= Tick;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(
                    ReportDirectory + "/failure.txt",
                    ex.ToString());
                Debug.LogError(
                    "P3.3 closeout validation failed: " + ex);
                if (s_window != null)
                {
                    s_window.Close();
                    s_window = null;
                }
                EditorApplication.update -= Tick;
            }
        }

        private static void BeginVisualValidation()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException(
                    "Active Scene is dirty; validation refused to save or "
                    + "discard user changes.");
            }

            s_window =
                ScriptableObject.CreateInstance<
                    CharacterTestP33ValidationWindow>();
            s_window.titleContent =
                new GUIContent("P3.3 Trigger Visual Validation");
            s_window.position = new Rect(
                20f,
                20f,
                1000f,
                500f);
            s_window.Show();
            s_window.Focus();
            s_phase = 1;
            s_frames = 0;
        }

        private static void WriteVisualReport()
        {
            File.WriteAllText(
                VisualReportPath,
                "status: captured\n"
                + "standard: "
                + StandardCapturePath
                + "\n"
                + "narrow: "
                + NarrowCapturePath
                + "\n"
                + "playerHp: "
                + PlayerHpCapturePath
                + "\n");
        }

        private static void StartEditModeTests()
        {
            MxLevelEditorWindow[] windows =
                UnityEngine.Resources
                    .FindObjectsOfTypeAll<MxLevelEditorWindow>();
            s_reopenLevelEditorAfterTests = windows.Length > 0;
            for (int i = 0; i < windows.Length; i++)
                windows[i].Close();

            string[] tests = DiscoverTests(
                "MxFramework.Tests",
                new[]
                {
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestGameplayTriggerWorkspaceTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestGameplayTriggerWorkspaceInteractionTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestGameplayTriggerWorkspaceTransactionTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestGameplayTriggerWorkspacePersistenceTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestGameplayTriggerWorkspaceSceneIntegrationTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestLevelGameplayTriggerAuthoringTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "CharacterTestLevelGameplayTriggerRuntimeTests",
                    "MxFramework.Tests.Game.CharacterTest."
                    + "MxLevelEditorSaveCoordinatorTests"
                });
            s_editRun = CharacterTestPerformanceUnityTestReportRun.Start(
                EditModeReportPath,
                "P3.3 a-e closeout",
                tests,
                TestMode.EditMode,
                "EditMode");
        }

        private static void StartPlayModeTests()
        {
            string[] tests = DiscoverTests(
                "MxFramework.Tests.PlayMode.CharacterTest",
                new[]
                {
                    "MxFramework.Tests.PlayMode.CharacterTest."
                    + "CharacterTestLevelGameplayTriggerPlayModeTests"
                });
            CharacterTestPerformanceUnityTestReportRun.Start(
                PlayModeReportPath,
                "P3.3 a-c runtime regression",
                tests,
                TestMode.PlayMode,
                "PlayMode");
        }

        private static string[] DiscoverTests(
            string assemblyName,
            IReadOnlyList<string> fixtureNames)
        {
            Assembly targetAssembly = null;
            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        assemblyName,
                        StringComparison.Ordinal))
                {
                    targetAssembly = assemblies[i];
                    break;
                }
            }
            if (targetAssembly == null)
            {
                throw new InvalidOperationException(
                    "Test assembly is not loaded: " + assemblyName);
            }

            var names = new List<string>();
            for (int fixtureIndex = 0;
                 fixtureIndex < fixtureNames.Count;
                 fixtureIndex++)
            {
                Type fixture =
                    targetAssembly.GetType(
                        fixtureNames[fixtureIndex],
                        throwOnError: true);
                MethodInfo[] methods = fixture.GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public);
                for (int methodIndex = 0;
                     methodIndex < methods.Length;
                     methodIndex++)
                {
                    if (!IsTestMethod(methods[methodIndex]))
                        continue;

                    names.Add(
                        fixture.FullName
                        + "."
                        + methods[methodIndex].Name);
                }
            }
            if (names.Count == 0)
            {
                throw new InvalidOperationException(
                    "No tests discovered for " + assemblyName);
            }
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            object[] attributes =
                method.GetCustomAttributes(inherit: true);
            for (int i = 0; i < attributes.Length; i++)
            {
                string name = attributes[i].GetType().FullName;
                if (string.Equals(
                        name,
                        "NUnit.Framework.TestAttribute",
                        StringComparison.Ordinal)
                    || string.Equals(
                        name,
                        "UnityEngine.TestTools.UnityTestAttribute",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void CaptureWindow(
            EditorWindow window,
            string relativePath)
        {
            Rect position = window.position;
            int width = Mathf.RoundToInt(position.width);
            int height = Mathf.RoundToInt(position.height);
            Color[] pixels =
                InternalEditorUtility.ReadScreenPixel(
                    position.position,
                    width,
                    height);
            if (pixels == null
                || pixels.Length != width * height)
            {
                throw new InvalidOperationException(
                    "Editor window capture returned an invalid pixel buffer.");
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply(
                    updateMipmaps: false,
                    makeNoLongerReadable: false);
                File.WriteAllBytes(
                    Path.GetFullPath(relativePath),
                    texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    internal sealed class CharacterTestP33ValidationWindow : EditorWindow
    {
        private CharacterTestWorldObjectLevelEditorModule _module;

        public void CreateGUI()
        {
            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/com.vincent.wgameframework/Editor/Levels/"
                    + "MxLevelEditorWindow.uss");
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            BuildPresenceWorkspace();
        }

        internal void ApplyNarrowWidth()
        {
            position = new Rect(
                position.x,
                position.y,
                760f,
                500f);
            _module.SetTriggerWorkspaceWidthForTests(760f);
            Focus();
            Repaint();
        }

        internal void ApplyPlayerHpStandard()
        {
            position = new Rect(
                position.x,
                position.y,
                1000f,
                500f);
            ResetWorkspace();
            var document =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                document.AddPlayerHpLowStoryTemplate(
                    CharacterTestStoryIds.Triggers.LevelStartRegion);
            document.TryUpdateConditionRoot(
                entry.StableId,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.Any,
                    children: new[]
                    {
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation
                                .CurrentScalar,
                            operand: 30L * Fix64.Scale,
                            comparison:
                                GameplayTriggerComparison.LessOrEqual),
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.Not,
                            children: new[]
                            {
                                new GameplayTriggerConditionNodeAuthoring(
                                    GameplayTriggerConditionOperation
                                        .HasSubject,
                                    operand: 1L)
                            })
                    }));
            _module.SetTriggerDocumentForTests(
                document,
                markDirty: false);
            rootVisualElement.Add(
                _module.CreateGameplayTriggerWorkspace(null));
            _module.SetTriggerWorkspaceWidthForTests(1000f);
            Focus();
            Repaint();
        }

        private void BuildPresenceWorkspace()
        {
            ResetWorkspace();
            var document =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                document.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            document.TryUpdateConditionRoot(
                entry.StableId,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.Any,
                    children: new[]
                    {
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.HasSubject,
                            operand: 1L),
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.Not,
                            children: new[]
                            {
                                new GameplayTriggerConditionNodeAuthoring(
                                    GameplayTriggerConditionOperation
                                        .HasSubject,
                                    operand: 1L)
                            })
                    }));
            _module.SetTriggerDocumentForTests(
                document,
                markDirty: false);
            rootVisualElement.Add(
                _module.CreateGameplayTriggerWorkspace(null));
            _module.SetTriggerWorkspaceWidthForTests(position.width);
        }

        private void ResetWorkspace()
        {
            rootVisualElement.Clear();
            _module =
                new CharacterTestWorldObjectLevelEditorModule();
            _module.OnEnable();
        }

        private void OnDisable()
        {
            _module = null;
        }
    }
}
