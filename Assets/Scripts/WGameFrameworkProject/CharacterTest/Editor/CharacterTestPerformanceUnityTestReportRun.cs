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
    internal sealed class CharacterTestPerformanceUnityTestReportRun : ICallbacks
    {
        private readonly TestRunnerApi _api;
        private readonly List<ITestResultAdaptor> _leafResults;
        private readonly string _scenarioName;
        private readonly TestMode _testMode;
        private readonly string _modeLabel;
        private readonly string[] _testNames;
        private readonly DateTime _startedUtc;

        private CharacterTestPerformanceUnityTestReportRun(
            string reportPath,
            string scenarioName,
            string[] testNames,
            TestMode testMode,
            string modeLabel)
        {
            ReportPath = reportPath;
            _scenarioName = scenarioName;
            _testMode = testMode;
            _modeLabel = string.IsNullOrWhiteSpace(modeLabel) ? testMode.ToString() : modeLabel;
            _testNames = testNames;
            _leafResults = new List<ITestResultAdaptor>(testNames.Length);
            _startedUtc = DateTime.UtcNow;
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.hideFlags = HideFlags.HideAndDontSave;
            _api.RegisterCallbacks(this);
        }

        public bool IsDone { get; private set; }
        public string ReportPath { get; }

        /// <remarks>AllocByDesign: editor-only explicit validation report generation.</remarks>
        public static CharacterTestPerformanceUnityTestReportRun Start(
            string reportPath,
            string scenarioName,
            string[] testNames,
            TestMode testMode = TestMode.EditMode,
            string modeLabel = null)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var run = new CharacterTestPerformanceUnityTestReportRun(
                reportPath,
                scenarioName,
                testNames,
                testMode,
                modeLabel);
            try
            {
                SaveDirtyScenesIfNeeded();
                var filter = new Filter
                {
                    testMode = testMode,
                    testNames = testNames
                };
                run._api.Execute(new ExecutionSettings(filter));
                EditorApplication.QueuePlayerLoopUpdate();
                return run;
            }
            catch
            {
                run.Release();
                throw;
            }
        }

        /// <remarks>AllocByDesign: editor-only explicit validation report generation.</remarks>
        public static void WriteLaunchFailureReport(
            string reportPath,
            string scenarioName,
            string[] testNames,
            TestMode testMode,
            string modeLabel,
            Exception exception)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string safeModeLabel = string.IsNullOrWhiteSpace(modeLabel)
                ? testMode.ToString()
                : modeLabel;
            var builder = new StringBuilder(1024);
            builder.Append("scenario: ").AppendLine(scenarioName);
            builder.Append("mode: ").AppendLine(safeModeLabel);
            builder.AppendLine("status: launch-failed");
            builder.Append("requestedTests: ").AppendLine(testNames.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append("exception: ").AppendLine(exception.GetType().FullName);
            builder.Append("message: ").AppendLine(exception.Message);
            File.WriteAllText(reportPath, builder.ToString());
        }

        /// <remarks>AllocByDesign: editor-only explicit validation report generation.</remarks>
        public static void WriteLaunchFailureReport(
            string reportPath,
            string scenarioName,
            string[] testNames,
            Exception exception)
        {
            WriteLaunchFailureReport(
                reportPath,
                scenarioName,
                testNames,
                TestMode.EditMode,
                null,
                exception);
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _leafResults.Clear();
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result == null || result.HasChildren)
                return;

            _leafResults.Add(result);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                if (_leafResults.Count == 0 && result != null)
                    CollectLeafResults(result, _leafResults);

                File.WriteAllText(ReportPath, CreateReportText(result, DateTime.UtcNow));
                Debug.Log("CharacterTest performance Unity test report written: " + ReportPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("CharacterTest focused performance Unity test report failed: " + ex);
            }
            finally
            {
                Release();
            }
        }

        private static void CollectLeafResults(ITestResultAdaptor result, List<ITestResultAdaptor> output)
        {
            if (result == null)
                return;

            if (!result.HasChildren)
            {
                output.Add(result);
                return;
            }

            foreach (ITestResultAdaptor child in result.Children)
                CollectLeafResults(child, output);
        }

        private string CreateReportText(ITestResultAdaptor result, DateTime finishedUtc)
        {
            var builder = new StringBuilder(4096);
            int passed = result == null ? CountResults("Passed") : result.PassCount;
            int failed = result == null ? CountFailedResults() : result.FailCount;
            int skipped = result == null ? CountSkippedResults() : result.SkipCount;
            int inconclusive = result == null ? CountResults("Inconclusive") : result.InconclusiveCount;
            int total = passed + failed + skipped + inconclusive;

            builder.Append("scenario: ").AppendLine(_scenarioName);
            builder.Append("mode: ").AppendLine(_modeLabel);
            builder.AppendLine("status: finished");
            builder.Append("startedUtc: ").AppendLine(_startedUtc.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("finishedUtc: ").AppendLine(finishedUtc.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("durationSeconds: ").AppendLine((finishedUtc - _startedUtc).TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("requestedTests: ").AppendLine(_testNames.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append("resultState: ").AppendLine(result == null ? "Unknown" : result.ResultState);
            builder.Append("summary.total: ").AppendLine(total.ToString(CultureInfo.InvariantCulture));
            builder.Append("summary.passed: ").AppendLine(passed.ToString(CultureInfo.InvariantCulture));
            builder.Append("summary.failed: ").AppendLine(failed.ToString(CultureInfo.InvariantCulture));
            builder.Append("summary.skipped: ").AppendLine(skipped.ToString(CultureInfo.InvariantCulture));
            builder.Append("summary.inconclusive: ").AppendLine(inconclusive.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("results:");

            for (int i = 0; i < _leafResults.Count; i++)
            {
                ITestResultAdaptor item = _leafResults[i];
                builder.Append("- state=").Append(item.ResultState);
                builder.Append(" durationSeconds=").Append(item.Duration.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" name=").AppendLine(SafeFullName(item));
                if (!IsPassingResult(item.ResultState) && !string.IsNullOrEmpty(item.Message))
                    builder.Append("  message: ").AppendLine(item.Message.Replace('\n', ' '));
            }

            return builder.ToString();
        }

        private int CountResults(string state)
        {
            int count = 0;
            for (int i = 0; i < _leafResults.Count; i++)
            {
                if (string.Equals(_leafResults[i].ResultState, state, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        private int CountFailedResults()
        {
            int count = 0;
            for (int i = 0; i < _leafResults.Count; i++)
            {
                string state = _leafResults[i].ResultState;
                if (!string.IsNullOrEmpty(state) && state.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }

            return count;
        }

        private int CountSkippedResults()
        {
            int count = 0;
            for (int i = 0; i < _leafResults.Count; i++)
            {
                string state = _leafResults[i].ResultState;
                if (!string.IsNullOrEmpty(state) && state.IndexOf("Skipped", StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }

            return count;
        }

        private static bool IsPassingResult(string state)
        {
            return string.Equals(state, "Passed", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeFullName(ITestResultAdaptor result)
        {
            if (result == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(result.FullName))
                return result.FullName;

            return result.Name ?? string.Empty;
        }

        private static void SaveDirtyScenesIfNeeded()
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene =
                    UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isDirty || string.IsNullOrEmpty(scene.path))
                    continue;

                EditorSceneManager.SaveScene(scene);
            }
        }

        private void Release()
        {
            IsDone = true;
            if (_api == null)
                return;

            _api.UnregisterCallbacks(this);
            ScriptableObject.DestroyImmediate(_api);
        }
    }
}
