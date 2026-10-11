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
    public static class CharacterTestPerformanceWarmRunTimingReportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Write Warm Run Timing Report";
        public const string DefaultReportPath = "Temp/CharacterTestPerformanceWarmRunTimingReport.txt";
        public const string DefaultScenarioName = "character-test.editor-menu.warm-run-timing";
        public const int DefaultWarmupFrameCount = CharacterTestPerformanceWarmRunTimingReport.DefaultWarmupFrameCount;
        public const int DefaultCaptureFrameCount = CharacterTestPerformanceWarmRunTimingReport.DefaultCaptureFrameCount;
        public const double DefaultDeltaTimeSeconds = CharacterTestPerformanceWarmRunTimingReport.DefaultDeltaTimeSeconds;

        [MenuItem(MenuPath)]
        public static void WriteDefaultWarmRunTimingReport()
        {
            string path = WriteWarmRunTimingReport(
                DefaultReportPath,
                DefaultScenarioName,
                DefaultWarmupFrameCount,
                DefaultCaptureFrameCount,
                DefaultDeltaTimeSeconds);
            Debug.Log("CharacterTest Warm Run Timing Report written: " + path);
        }

        /// <remarks>AllocByDesign: editor-only explicit diagnostics/profile evidence capture.</remarks>
        public static string WriteWarmRunTimingReport(
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
                CreateWarmRunTimingReportText(
                    scenarioName,
                    warmupFrameCount,
                    captureFrameCount,
                    deltaTimeSeconds));
            return fullPath;
        }

        /// <remarks>AllocByDesign: editor-only explicit diagnostics/profile evidence capture.</remarks>
        public static string CreateWarmRunTimingReportText(
            string scenarioName,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds)
        {
            return CharacterTestPerformanceWarmRunTimingReport.CreateText(
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
