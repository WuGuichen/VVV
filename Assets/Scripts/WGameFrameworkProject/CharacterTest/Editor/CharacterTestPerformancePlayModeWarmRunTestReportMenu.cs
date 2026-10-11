using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    public static class CharacterTestPerformancePlayModeWarmRunTestReportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Run Play Mode Warm Run Test";
        public const string DefaultReportPath = "Temp/CharacterTestPerformancePlayModeUnityTestReport.txt";
        public const string DefaultScenarioName = "character-test.performance.playmode-warm-run-test";

        private static readonly string[] PlayModeWarmRunTestNames =
        {
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.CharacterTestScene_PlayModeWarmRunReport_WritesSceneTimingAndCountersIntoTemp"
        };

        private static CharacterTestPerformanceUnityTestReportRun _activeRun;

        [MenuItem(MenuPath)]
        public static void RunPlayModeWarmRunTest()
        {
            if (_activeRun != null && !_activeRun.IsDone)
            {
                Debug.LogWarning(
                    "CharacterTest Play Mode warm-run Unity test is already running. Report path: "
                    + _activeRun.ReportPath);
                return;
            }

            string fullPath = ResolveReportPath(DefaultReportPath);
            try
            {
                _activeRun = CharacterTestPerformanceUnityTestReportRun.Start(
                    fullPath,
                    DefaultScenarioName,
                    PlayModeWarmRunTestNames,
                    TestMode.EditMode,
                    "EditMode(EnterPlayMode)");
                Debug.Log("CharacterTest Play Mode warm-run Unity test started. Report path: " + fullPath);
            }
            catch (Exception ex)
            {
                CharacterTestPerformanceUnityTestReportRun.WriteLaunchFailureReport(
                    fullPath,
                    DefaultScenarioName,
                    PlayModeWarmRunTestNames,
                    TestMode.EditMode,
                    "EditMode(EnterPlayMode)",
                    ex);
                Debug.LogError("CharacterTest Play Mode warm-run Unity test failed to start: " + ex.Message);
            }
        }

        public static string ResolveReportPath(string reportPath)
        {
            string safePath = string.IsNullOrWhiteSpace(reportPath)
                ? DefaultReportPath
                : reportPath;
            return Path.GetFullPath(safePath);
        }
    }
}
