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
    public static class CharacterTestEditorToolBootstrap
    {
        static CharacterTestEditorToolBootstrap()
        {
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-smoke-report",
                    "CharacterTest 性能 Smoke Report",
                    "CharacterTest",
                    "运行固定帧 CharacterTest smoke scenario 并把性能 counter report 写入 Temp。",
                    "菜单",
                    CharacterTestPerformanceSmokeReportMenu.MenuPath,
                    70,
                    CharacterTestPerformanceSmokeReportMenu.WriteDefaultSmokeReport));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-focused-tests",
                    "CharacterTest 性能聚焦测试",
                    "CharacterTest",
                    "运行本次性能优化相关的 EditMode 聚焦测试并把结果写入 Temp。",
                    "菜单",
                    CharacterTestPerformanceUnityTestReportMenu.MenuPath,
                    71,
                    CharacterTestPerformanceUnityTestReportMenu.RunFocusedEditModeTests));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-warm-run-timing",
                    "CharacterTest Warm Run Timing",
                    "CharacterTest",
                    "运行固定帧 CharacterTest clean warm-run timing scenario 并把耗时/GC report 写入 Temp。",
                    "菜单",
                    CharacterTestPerformanceWarmRunTimingReportMenu.MenuPath,
                    72,
                    CharacterTestPerformanceWarmRunTimingReportMenu.WriteDefaultWarmRunTimingReport));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.refresh-player-runtime-catalogs",
                    "CharacterTest Player Runtime Catalog Artifacts",
                    "CharacterTest",
                    "刷新 CharacterTest generated runtime catalog 到 StreamingAssets Player mirror。",
                    "菜单",
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.MenuPath,
                    73,
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.RefreshDefaultMenu));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.repair-runtime-catalog-artifacts",
                    "CharacterTest Runtime Catalog Repair",
                    "CharacterTest",
                    "重建 ability/buff runtime .bytes，刷新 StreamingAssets Player mirror，并校验 JSON/binary 一致性。",
                    "菜单",
                    CharacterTestRuntimeCatalogArtifactRepairTool.MenuPath,
                    74,
                    CharacterTestRuntimeCatalogArtifactRepairTool.RepairDefaultMenu));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-playmode-warm-run-test",
                    "CharacterTest Play Mode Warm Run",
                    "CharacterTest",
                    "运行 CharacterTestScene Play Mode warm-run 测试并把测试汇总与性能 report 写入 Temp。",
                    "菜单",
                    CharacterTestPerformancePlayModeWarmRunTestReportMenu.MenuPath,
                    75,
                    CharacterTestPerformancePlayModeWarmRunTestReportMenu.RunPlayModeWarmRunTest));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-player-build-run",
                    "CharacterTest Player Warm Run",
                    "CharacterTest",
                    "构建并运行 CharacterTestScene Development Player，把 Player warm-run 与 ProfilerRecorder report 写入 Temp。",
                    "菜单",
                    CharacterTestPerformancePlayerBuildReportMenu.MenuPath,
                    76,
                    CharacterTestPerformancePlayerBuildReportMenu.BuildAndRunDefaultPlayerReport));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.runtime-catalog-load-benchmark",
                    "CharacterTest Runtime Catalog Load Benchmark",
                    "CharacterTest",
                    "对 CharacterTest generated runtime catalog 分别执行 JSON/binary import 并写入加载耗时与 GC 报告。",
                    "菜单",
                    CharacterTestRuntimeCatalogLoadBenchmark.MenuPath,
                    77,
                    CharacterTestRuntimeCatalogLoadBenchmark.WriteDefaultReport));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-profiler-raw-csv",
                    "CharacterTest Profiler Raw CSV",
                    "CharacterTest",
                    "从当前 Unity Profiler 缓冲区导出 frameTime 与 markerTiming CSV 到 Temp/ProfileAnalyzer。",
                    "菜单",
                    CharacterTestPerformanceProfilerRawCsvExportMenu.MenuPath,
                    78,
                    CharacterTestPerformanceProfilerRawCsvExportMenu.ExportDefaultProfilerRawCsv));
            MxFramework.Editor.FrameworkManagerToolRegistry.Register(
                new MxFramework.Editor.FrameworkManagerToolInfo(
                    "character-test.performance-profiler-raw-csv-tornado-window",
                    "CharacterTest Profiler Tornado Window CSV",
                    "CharacterTest",
                    "从当前 Unity Profiler 缓冲区定位 Tornado marker，并导出附近 frameTime 与 markerTiming CSV。",
                    "菜单",
                    CharacterTestPerformanceProfilerRawCsvExportMenu.TornadoWindowMenuPath,
                    79,
                    CharacterTestPerformanceProfilerRawCsvExportMenu.ExportDefaultTornadoWindowProfilerRawCsv));
        }
    }
}
