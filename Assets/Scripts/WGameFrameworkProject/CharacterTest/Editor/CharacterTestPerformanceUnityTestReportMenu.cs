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
    public static class CharacterTestPerformanceUnityTestReportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Run Focused Unity Tests";
        public const string DefaultReportPath = "Temp/CharacterTestPerformanceUnityTestReport.txt";
        public const string DefaultScenarioName = "character-test.performance.focused-editmode";

        private static readonly string[] FocusedEditModeTestNames =
        {
            "MxFramework.Tests.Combat.Physics.CombatPhysicsWorldTests.ColliderLookup_RebuildsWhenRemovingAndReplacingShapes",
            "MxFramework.Tests.CharacterRuntimeSpawn.CharacterRuntimeResourceBootstrapTests.AnimationResources_CreateCatalogEntries_ReusesParsedClipRegistryData",
            "MxFramework.Tests.CharacterRuntimeSpawn.CharacterRuntimeResourceBootstrapTests.LoadCharacter_WarmsCompiledAnimationResourcesBeforeSpawn",
            "MxFramework.Tests.CharacterRuntimeSpawn.CharacterRuntimeInputMotionControllerTests.LocomotionBlend_AnimatorParametersSkipUnchangedValues",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterMotion_FrameworkPhysicsWorld_CombatColliderSyncSkipsUnchangedState",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterRuntimeView_SyncTransformSkipsUnchangedPose",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterRuntimeView_VfxSocketAnchorResolutionIsCached",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterRuntimeView_RepeatedAnimationSyncDoesNotAllocateAfterWarmup",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestDynamicFieldTargetIndex_TornadoCapsuleMatchesCombatQueryForDynamicTargets",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryOrderIsStable",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryDoesNotAllocateAfterWarmup",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestPerformanceCounterReport_ExportsStableSmokeReport",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestPerformanceSmokeScenario_RunsFixedFramesAndExportsCounters",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.FairyGuiCombatHud_SignatureCreateWarmPathDoesNotAllocate",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.FairyGuiCombatHud_BuildIntoStableHotbarDoesNotAllocateAfterWarmup",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.FairyGuiCombatHudBinder_WarmBindDoesNotAllocateStatusText",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.FairyGuiStoryDialogHost_RefreshSkipsSameViewModelAndClosesWhenStoryIsIdle",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestIntegrationContractTests.CharacterTestUiRuntime_ModalPanelRefreshScheduler_SkipsClosedAndThrottlesOpenPanels",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestFloatingCombatTextTests.MainCameraCache_ReusesValidCameraAndRefreshesWhenDisabled",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestFloatingCombatTextTests.HeadPad_PrewarmPoolCreatesDetachedReusablePads",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestFloatingCombatTextTests.UiRuntime_ConfigureFairyGuiPrewarmsHeadPadPool",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformanceSmokeReportMenu_WritesStableReportIntoTemp",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformanceWarmRunTimingReportMenu_WritesTimingAndAllocationStatsIntoTemp",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformanceProfilerRawCsvExportMenu_WritesCsvHeadersAndSummaryIntoTemp",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformanceProfilerRawCsvExportMenu_ResolvesMarkerCenteredWindow",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformanceProfilerRawCsvExportMenu_WritesMarkerWindowSummaryWhenMarkerIsMissing",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.PerformancePlayerReportRunner_CommandLineParserResolvesReportDirectory",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.CombatHudNetworkStatusProvider_DoesNotAllocateWhenNetworkInactive",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.CombatHudBuffBuildInto_WithReusableBuffersDoesNotAllocateAfterWarmup",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.CombatHudBuildInto_WithStableActionTextDoesNotAllocateAfterWarmup",
            "MxFramework.Tests.Game.CharacterTest.CharacterTestProfilerGcRemediationTests.RuntimeTick_WithStableMoveInputDoesNotAllocateAfterWarmup"
        };

        private static CharacterTestPerformanceUnityTestReportRun _activeRun;

        [MenuItem(MenuPath)]
        public static void RunFocusedEditModeTests()
        {
            if (_activeRun != null && !_activeRun.IsDone)
            {
                Debug.LogWarning(
                    "CharacterTest focused performance Unity tests are already running. Report path: "
                    + _activeRun.ReportPath);
                return;
            }

            string fullPath = ResolveReportPath(DefaultReportPath);
            try
            {
                _activeRun = CharacterTestPerformanceUnityTestReportRun.Start(
                    fullPath,
                    DefaultScenarioName,
                    FocusedEditModeTestNames);
                Debug.Log("CharacterTest focused performance Unity tests started. Report path: " + fullPath);
            }
            catch (Exception ex)
            {
                CharacterTestPerformanceUnityTestReportRun.WriteLaunchFailureReport(
                    fullPath,
                    DefaultScenarioName,
                    FocusedEditModeTestNames,
                    ex);
                Debug.LogError("CharacterTest focused performance Unity tests failed to start: " + ex.Message);
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
