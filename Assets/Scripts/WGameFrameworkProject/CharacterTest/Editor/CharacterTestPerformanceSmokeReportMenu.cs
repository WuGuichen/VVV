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
    public static class CharacterTestPerformanceSmokeReportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Write Smoke Report";
        public const string DefaultReportPath = "Temp/CharacterTestPerformanceSmokeReport.txt";
        public const string DefaultScenarioName = "character-test.editor-menu.smoke";
        public const int DefaultWarmupFrameCount = CharacterTestPerformanceSmokeReport.DefaultWarmupFrameCount;
        public const int DefaultCaptureFrameCount = CharacterTestPerformanceSmokeReport.DefaultCaptureFrameCount;
        public const double DefaultDeltaTimeSeconds = CharacterTestPerformanceSmokeReport.DefaultDeltaTimeSeconds;

        [MenuItem(MenuPath)]
        public static void WriteDefaultSmokeReport()
        {
            string path = WriteSmokeReport(
                DefaultReportPath,
                DefaultScenarioName,
                DefaultWarmupFrameCount,
                DefaultCaptureFrameCount,
                DefaultDeltaTimeSeconds);
            Debug.Log("CharacterTest Performance Smoke Report written: " + path);
        }

        /// <remarks>AllocByDesign: editor-only explicit diagnostics/profile evidence capture.</remarks>
        public static string WriteSmokeReport(
            string reportPath,
            string scenarioName,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds)
        {
            string fullPath = ResolveReportPath(reportPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                fullPath,
                CreateSmokeReportText(
                    scenarioName,
                    warmupFrameCount,
                    captureFrameCount,
                    deltaTimeSeconds));
            return fullPath;
        }

        /// <remarks>AllocByDesign: editor-only explicit diagnostics/profile evidence capture.</remarks>
        public static string CreateSmokeReportText(
            string scenarioName,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds)
        {
            return CharacterTestPerformanceSmokeReport.CreateText(
                scenarioName,
                warmupFrameCount,
                captureFrameCount,
                deltaTimeSeconds);
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
