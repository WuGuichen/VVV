using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabSessionRecoverySnapshot_DeepCopiesMutableHistoryStores()
        {
            var host = new GameObject("Runtime AI Lab Recovery Snapshot Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Type controllerType = typeof(CharacterTestRuntimeAiPlannerDevSceneController);
                MethodInfo captureMethod = controllerType.GetMethod(
                    "CaptureSessionStartRecoverySnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo eventIndexField = controllerType.GetField(
                    "_timelineEventIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo captureStoreField = controllerType.GetField(
                    "_testStateCaptures",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(captureMethod);
                Assert.IsNotNull(eventIndexField);
                Assert.IsNotNull(captureStoreField);

                var eventIndex = (CharacterTestRuntimeAiLabTimelineEventIndex)
                    eventIndexField.GetValue(controller);
                eventIndex.Reset(new CharacterTestRuntimeAiLabTimelineSessionIdentity(4, 20, 8));
                eventIndex.CaptureFrame(
                    20,
                    null,
                    new[]
                    {
                        new CharacterTestRuntimeEvent(
                            new RuntimeFrame(20),
                            CharacterTestRuntimeEventType.CommandAccepted,
                            commandId: 301,
                            sourceId: 3,
                            targetId: 7)
                    },
                    null,
                    null);

                var captureStore = (RuntimeAiLabTestStateCaptureStore)
                    captureStoreField.GetValue(controller);
                captureStore.Toggle();
                Assert.IsTrue(captureStore.TryAdd(
                    new RuntimeAiLabTestStateCapture(
                        new RuntimeFrame(20),
                        0.333d,
                        null,
                        null,
                        null)));

                object snapshot = captureMethod.Invoke(controller, null);
                PropertyInfo timelineEventsProperty = snapshot.GetType().GetProperty(
                    "TimelineEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo testCapturesProperty = snapshot.GetType().GetProperty(
                    "TestStateCaptures",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(timelineEventsProperty);
                Assert.IsNotNull(testCapturesProperty);
                var capturedEvents =
                    (CharacterTestRuntimeAiLabTimelineEventIndex.RecoveryState)
                    timelineEventsProperty.GetValue(snapshot);
                var capturedTests =
                    (RuntimeAiLabTestStateCaptureStore.RecoveryState)
                    testCapturesProperty.GetValue(snapshot);
                long expectedEventHash = capturedEvents.ContentHash;
                long expectedTestHash = capturedTests.ContentHash;

                eventIndex.CaptureFrame(
                    20,
                    null,
                    new[]
                    {
                        new CharacterTestRuntimeEvent(
                            new RuntimeFrame(20),
                            CharacterTestRuntimeEventType.CommandAccepted,
                            commandId: 999,
                            sourceId: 9,
                            targetId: 9)
                    },
                    null,
                    null);
                captureStore.Clear();

                Assert.AreNotEqual(expectedEventHash, eventIndex.ComputeRecoveryHash());
                Assert.AreNotEqual(expectedTestHash, captureStore.ComputeRecoveryHash());
                Assert.AreEqual(expectedEventHash, capturedEvents.ContentHash);
                Assert.AreEqual(expectedTestHash, capturedTests.ContentHash);

                var restoredEvents = new CharacterTestRuntimeAiLabTimelineEventIndex();
                var restoredTests = new RuntimeAiLabTestStateCaptureStore();
                restoredEvents.RestoreRecoveryState(capturedEvents);
                restoredTests.RestoreRecoveryState(capturedTests);
                Assert.AreEqual(expectedEventHash, restoredEvents.ComputeRecoveryHash());
                Assert.AreEqual(expectedTestHash, restoredTests.ComputeRecoveryHash());
                Assert.AreEqual(1, restoredEvents.CapturedFrameCount);
                Assert.AreEqual(1, restoredTests.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabPanelCommandSink_HideUsesReachableCallbackWithoutLauncher()
        {
            var host = new GameObject("Runtime AI Lab Visibility Command Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                int hideCount = 0;
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                    controller,
                    () =>
                    {
                        hideCount++;
                        return true;
                    });

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close,
                    null));
                Assert.AreEqual(1, hideCount);
                StringAssert.Contains("F9", sink.LastStatusMessage);

            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabPanelBinder_NewRootDropsDisposedTimelinePoolWithoutTouchingIt()
        {
            var binder = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(null);
            FieldInfo poolField = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder)
                .GetField("_timelineMarkerPool", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo reset = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder)
                .GetMethod("ResetDynamicVisualsForNewRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(poolField);
            Assert.IsNotNull(reset);

            var staleVisual = new global::FairyGUI.GComponent();
            staleVisual.Dispose();
            var pool = (IList)poolField.GetValue(binder);
            pool.Add(staleVisual);

            var replacementRoot = new global::FairyGUI.GComponent();
            try
            {
                Assert.DoesNotThrow(() => reset.Invoke(binder, new object[] { replacementRoot }));
                Assert.AreEqual(0, pool.Count);
            }
            finally
            {
                replacementRoot.Dispose();
            }
        }

        [Test]
        public void RuntimeAiLabInput_UsesUnreservedF9Shortcut()
        {
            MethodInfo toKeyCode = typeof(CharacterTestRuntimeAiLabInput)
                .GetMethod("ToKeyCode", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(toKeyCode);
            Assert.AreEqual(
                KeyCode.F9,
                toKeyCode.Invoke(null, new object[] { CharacterTestRuntimeAiLabKey.F9 }));
        }


        [Test]
        public void RuntimeAiLabPanelSourcePackage_DeclaresBehaviorCardLibraryAndTimelineWorkspace()
        {
            string composerRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "ArtSource/UIKits/MxUiFoundation");
            string packageRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestRuntimeAiLabUi");
            string recipeText = File.ReadAllText(Path.Combine(
                composerRoot,
                "ui-component-recipes.json"));
            string screenText = File.ReadAllText(Path.Combine(
                composerRoot,
                "screens/character_test_runtime_ai_lab.ui-screen.json"));
            string contractText = File.ReadAllText(Path.Combine(
                composerRoot,
                "contracts/character_test_runtime_ai_lab.ui-composer-binding-contract.json"));
            string packageText = File.ReadAllText(Path.Combine(packageRoot, "package.xml"));
            string panelText = File.ReadAllText(Path.Combine(packageRoot, "RuntimeAiPlannerDevPanel.xml"));
            string libraryText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorCardLibrary.xml"));
            string timelineText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorTimeline.xml"));
            string timelineDetailsText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabTimeline.xml"));
            string trackText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabTimelineTrack.xml"));
            string runtimeHeaderText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorCardRuntimeHeader.xml"));
            string behaviorCardText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorCard.xml"));
            string triggerInspectorText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorRecipeInspector.xml"));
            string triggerConditionRowText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabBehaviorTriggerConditionRow.xml"));
            string behaviorGroupRowVisualText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabSetupBehaviorGroupRowVisual.xml"));
            string workspacePickerText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabAuthoringWorkspacePicker.xml"));
            string workspaceRoleRowText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabAuthoringWorkspaceRoleRow.xml"));
            string publishPopupText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabPublishCurrentChangesPopup.xml"));
            string navigationFeedbackText = File.ReadAllText(Path.Combine(
                packageRoot,
                "Components/RuntimeAiLabNavigationFeedbackOverlay.xml"));

            StringAssert.Contains("RuntimeAiLabBehaviorCardLibrary.xml", packageText);
            StringAssert.Contains("RuntimeAiLabBehaviorTimeline.xml", packageText);
            StringAssert.Contains("RuntimeAiLabTimelineTrack.xml", packageText);
            StringAssert.Contains("RuntimeAiLabTimelineSpan.xml", packageText);
            StringAssert.Contains("RuntimeAiLabTimelineMarker.xml", packageText);
            StringAssert.Contains("RuntimeAiLabBehaviorRecipeInspector.xml", packageText);
            StringAssert.Contains("RuntimeAiLabBehaviorTriggerConditionRow.xml", packageText);
            StringAssert.DoesNotContain("RuntimeAiLabTimelineStageCard.xml", packageText);
            StringAssert.DoesNotContain("RuntimeAiLabTimelineDetectorBand.xml", packageText);
            StringAssert.DoesNotContain("RuntimeAiLabTimelineTransitionChip.xml", packageText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringContextBar);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringGroupText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringCardText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringProblemText);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChangesPopup);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NavigationFeedbackOverlay);
            AssertPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .AuthoringWorkspacePickerPrototype);
            StringAssert.Contains("<list", panelText);
            StringAssert.Contains("overflow=\"scroll\"", panelText);
            StringAssert.Contains("defaultItem=", panelText);
            StringAssert.DoesNotContain("btnWorkbenchEntry", panelText);
            AssertPanelChildDeclared(behaviorCardText, "inlineToggleEntry");
            AssertPanelChildDeclared(behaviorCardText, "inlineDeleteEntry");
            StringAssert.Contains("mx.uiScreenBlueprint.v3", screenText);
            StringAssert.Contains("\"type\": \"List\"", screenText);
            AssertPanelChildDeclared(libraryText, "libraryQuery");
            AssertPanelChildDeclared(libraryText, "btnLibraryFilter");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityAll");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityTargeting");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityEngage");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityDamage");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityFallback");
            AssertPanelChildDeclared(libraryText, "assemblyResponsibilityOther");
            AssertPanelChildDeclared(libraryText, "assemblyClose");
            StringAssert.DoesNotContain("assemblyCategory", libraryText);
            AssertPanelChildDeclared(timelineText, "timelineGroupHost");
            AssertPanelChildDeclared(timelineText, "timelineGroupPrototype");
            AssertPanelChildDeclared(timelineText, "trackPrototype");
            AssertPanelChildDeclared(timelineText, "spanPrototype");
            AssertPanelChildDeclared(timelineText, "markerPrototype");
            AssertPanelChildDeclared(timelineText, "timelinePlayhead");
            AssertPanelChildDeclared(timelineDetailsText, "behaviorRecipeInspector");
            AssertPanelChildDeclared(triggerInspectorText, "conditionList");
            AssertPanelChildDeclared(triggerInspectorText, "btnOutcomeReturn");
            StringAssert.DoesNotContain("name=\"sourceLabel\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"btnSourcePredicate\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"btnSourceFrame\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"btnSourceSignal\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"btnAdvanced\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"listenLabel\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"inputStartFrame\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"btnWindowMode\"", triggerInspectorText);
            StringAssert.DoesNotContain("name=\"inputDuration\"", triggerInspectorText);
            StringAssert.Contains("<list", triggerInspectorText);
            StringAssert.Contains("overflow=\"scroll\"", triggerInspectorText);
            StringAssert.Contains("RuntimeAiLabBehaviorTriggerConditionRow", packageText);
            AssertPanelChildDeclared(triggerConditionRowText, "btnFact");
            AssertPanelChildDeclared(triggerConditionRowText, "btnOperator");
            AssertPanelChildDeclared(triggerConditionRowText, "inputValue");
            StringAssert.Contains("input=\"true\"", triggerConditionRowText);
            StringAssert.Contains("behavior_recipe_inspector", contractText);
            StringAssert.Contains("condition_list", contractText);
            StringAssert.Contains("authoringContextBar", contractText);
            StringAssert.Contains("select_authoring_workspace_role", contractText);
            StringAssert.Contains("test_current_changes", contractText);
            StringAssert.Contains("publish_current_changes", contractText);
            AssertPanelChildDeclared(behaviorGroupRowVisualText, "publishStatus");
            AssertPanelChildDeclared(workspaceRoleRowText, "currentMark");
            AssertPanelChildDeclared(workspaceRoleRowText, "diagnosticText");
            AssertPanelChildDeclared(workspacePickerText, "roleMain");
            AssertPanelChildDeclared(workspacePickerText, "roleExperimentA");
            AssertPanelChildDeclared(workspacePickerText, "roleExperimentB");
            AssertPanelChildDeclared(workspacePickerText, "confirmationPanel");
            AssertPanelChildDeclared(publishPopupText, "publishPopupTitle");
            AssertPanelChildDeclared(publishPopupText, "btnPublishPopupPrimary");
            AssertPanelChildDeclared(publishPopupText, "btnPublishPopupSecondary");
            AssertPanelChildDeclared(navigationFeedbackText, "navigationFeedbackScrim");
            AssertPanelChildDeclared(navigationFeedbackText, "navigationFeedbackModal");
            AssertPanelChildDeclared(navigationFeedbackText, "btnNavigationRetrySave");
            StringAssert.Contains(
                "ui.component.runtime_ai.authoring_workspace_picker",
                contractText);
            StringAssert.Contains(
                "ui.component.runtime_ai.publish_current_changes_popup",
                contractText);
            StringAssert.Contains(
                "ui.component.runtime_ai.navigation_feedback_overlay",
                contractText);
            AssertPanelChildDeclared(trackText, "trackLabel");
            AssertPanelChildDeclared(trackText, "trackContent");
            StringAssert.Contains("timeline_group_host", contractText);
            StringAssert.Contains("local_playhead", contractText);
            AssertPanelChildDeclared(runtimeHeaderText, "runtimeScrubPending");
            AssertPanelChildDeclared(runtimeHeaderText, "runtimeScrubQueued");
            AssertPanelChildDeclared(runtimeHeaderText, "runtimeScrubHover");
            StringAssert.Contains("runtime_hover_marker", contractText);
            StringAssert.Contains("建立新未来将使旧历史失效", runtimeHeaderText);
            StringAssert.DoesNotContain("????", runtimeHeaderText);
            Assert.That(runtimeHeaderText.IndexOf('\uFFFD'), Is.EqualTo(-1),
                "Published runtime header must not contain Unicode replacement characters.");
            Assert.IsFalse(timelineText.Contains("name=\"btnStartMinus\""));
            Assert.IsFalse(timelineText.Contains("name=\"btnStartPlus\""));
            Assert.IsFalse(panelText.Contains("name=\"behaviorCardCompileView\""));
            Assert.IsFalse(panelText.Contains("name=\"gameplayCardEditor\""));
            StringAssert.DoesNotContain("RuntimeAiLabBehaviorCardFlow.xml", packageText);
            StringAssert.DoesNotContain("RuntimeAiLabGameplayCardEditor.xml", packageText);
            StringAssert.DoesNotContain("RuntimeAiLabLogicTimelineCanvas.xml", packageText);
            AssertRetiredLogicTimelinePrototypeAbsent(recipeText);
            AssertRetiredLogicTimelinePrototypeAbsent(screenText);
            AssertRetiredLogicTimelinePrototypeAbsent(contractText);
        }

        private static void AssertRetiredLogicTimelinePrototypeAbsent(string authoringText)
        {
            StringAssert.DoesNotContain("ui.component.button.logic.stage", authoringText);
            StringAssert.DoesNotContain("ui.component.button.logic.detector", authoringText);
            StringAssert.DoesNotContain("ui.component.button.logic.transition", authoringText);
            StringAssert.DoesNotContain("RuntimeAiLabLogicTimelineCanvas", authoringText);
            StringAssert.DoesNotContain("RuntimeAiLabLogicStageButton", authoringText);
            StringAssert.DoesNotContain("RuntimeAiLabLogicDetectorButton", authoringText);
            StringAssert.DoesNotContain("RuntimeAiLabLogicTransitionButton", authoringText);
        }

        [Test]
        public void RuntimeAiLabPreviewOverlay_DoesNotCreateWorldSpaceTextLabels()
        {
            string overlayText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevPreviewOverlay.cs");

            StringAssert.DoesNotContain("TextMesh", overlayText);
            StringAssert.DoesNotContain("Decision Label", overlayText);
            StringAssert.DoesNotContain("Status Label", overlayText);
        }

        [Test]
        public void RuntimeAiLabSetupOverlay_UsesScreenSpacePickingWithoutPhysicsRaycasts()
        {
            string overlayText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabSetupOverlay.cs");
            string inputText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabInput.cs");

            StringAssert.Contains("PrimitiveType.Capsule", overlayText);
            StringAssert.Contains("IsDraggingSlot", overlayText);
            StringAssert.Contains("TryPickSlotByScreenPosition", overlayText);
            StringAssert.Contains("TryProjectPointerToGroundPlane", overlayText);
            StringAssert.Contains("WorldToScreenPoint", overlayText);
            StringAssert.Contains("ScreenPointToRay", overlayText);
            StringAssert.Contains("Plane", overlayText);
            StringAssert.Contains("DragStartScreenThreshold", overlayText);
            StringAssert.Contains("LineRenderer", overlayText);
            StringAssert.Contains("FacingArrow", overlayText);
            StringAssert.Contains("T1·", overlayText);
            StringAssert.Contains("_pendingDragView", overlayText);
            StringAssert.Contains("sqrMagnitude", overlayText);
            StringAssert.Contains("CharacterTestRuntimeAiLabCameraResolver", overlayText);
            StringAssert.Contains("IsPointerBlockedByUi", overlayText);
            StringAssert.Contains("Stage.isTouchOnUI", inputText);
            StringAssert.Contains("UiRaycastResults.Count > 0", inputText);
            StringAssert.Contains("Spawn selection retained. Drag a character preview to move it.", overlayText);
            StringAssert.Contains("The current spawn selection was retained.", overlayText);
            StringAssert.Contains("Setup placement missed", overlayText);
            StringAssert.DoesNotContain("ClearSetupSpawnSelection", overlayText);
            StringAssert.DoesNotContain("RaycastNonAlloc", overlayText);
            StringAssert.DoesNotContain("PreviewPickVolume", overlayText);
            StringAssert.DoesNotContain("IsGroundCollider", overlayText);
            StringAssert.DoesNotContain("IsPointerOverRuntimeAiLabHudChrome", overlayText);
            StringAssert.DoesNotContain("TryBeginSelectedSlotGroundDrag", overlayText);
            StringAssert.DoesNotContain("SetSelectedSetupSpawnPosition(groundPosition)", overlayText);
            StringAssert.DoesNotContain("IsInteractiveUiObjectName", inputText);
            StringAssert.DoesNotContain("IsPointerOverGameObject", inputText);
            StringAssert.DoesNotContain("MarkerScreenPickRadius", overlayText);
            StringAssert.DoesNotContain("RuntimeAiLabLeftHudWidth", inputText);
            StringAssert.DoesNotContain("RuntimeAiLabTopHudHeight", inputText);
            StringAssert.DoesNotContain("RuntimeAiLabBottomHudHeight", inputText);
        }

        [Test]
        public void RuntimeAiLabSceneGenerator_DeclaresStandardArenaFixture()
        {
            string generatorPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CreateCharacterTestRuntimeAiLabScene.cs");

            string generatorText = File.ReadAllText(generatorPath);

            StringAssert.Contains("Runtime AI Lab Standard Arena", generatorText);
            StringAssert.Contains("LabArena_Ground_64x44", generatorText);
            StringAssert.Contains("64m x 44m | 1m grid", generatorText);
            StringAssert.Contains("GridMajor", generatorText);
            StringAssert.Contains("GridAccent", generatorText);
            StringAssert.Contains("LabWall_LOS_01", generatorText);
            StringAssert.Contains("LabWall_Choke_North", generatorText);
            StringAssert.Contains("LabWall_Choke_South", generatorText);
            StringAssert.Contains("LabZone_E_Hazard", generatorText);
            StringAssert.Contains("ExpectedStaticColliderCount = 10", generatorText);
            StringAssert.Contains("StaticRoughSurface", generatorText);
            StringAssert.Contains("WorldObjectSceneCache.Configure", generatorText);
            StringAssert.Contains("CharacterTestRuntimeAiLabSetupOverlay", generatorText);
            StringAssert.Contains("CharacterTestRuntimeAiLabSetupCameraController", generatorText);
        }

        [Test]
        public void RuntimeAiLabSceneAsset_ContainsStandardArenaFixture()
        {
            string scenePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity");

            Assert.IsTrue(File.Exists(scenePath), "Runtime AI Lab scene asset must be generated.");
            string sceneText = File.ReadAllText(scenePath);

            StringAssert.Contains("Runtime AI Lab Standard Arena", sceneText);
            StringAssert.Contains("LabArena_Ground_64x44", sceneText);
            StringAssert.Contains("64m x 44m | 1m grid", sceneText);
            StringAssert.Contains("LabWall_LOS_01", sceneText);
            StringAssert.Contains("LabWall_Choke_North", sceneText);
            StringAssert.Contains("LabWall_Choke_South", sceneText);
            StringAssert.Contains("LabZone_E_Hazard", sceneText);
            StringAssert.Contains("LabSpawn_Ally_01", sceneText);
            StringAssert.Contains("LabSpawn_Enemy_01", sceneText);
        }

        [Test]
        public void RuntimeAiLabSceneAsset_UsesSemanticWGameShaderMapping()
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity",
                OpenSceneMode.Single);

            var expected = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Runtime AI Lab Ground", "WGame/EnvironmentStylizedLit" },
                { "Runtime AI Lab Wall", "WGame/EnvironmentStylizedLit" },
                { "Runtime AI Lab Obstacle", "WGame/EnvironmentStylizedLit" },
                { "Runtime AI Lab Grid", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Grid Major", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Grid Accent", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Zone A", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Zone B", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Zone C", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Zone D", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Zone F", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Hazard", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Ally Spawn", "WGame/PropStylizedLit" },
                { "Runtime AI Lab Enemy Spawn", "WGame/PropStylizedLit" },
            };

            Material[] materials = UnityEngine.Resources.FindObjectsOfTypeAll<Material>();
            var actual = new System.Collections.Generic.Dictionary<string, string>();
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null ||
                    !expected.ContainsKey(material.name))
                    continue;
                actual[material.name] = material.shader.name;
            }

            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            foreach (System.Collections.Generic.KeyValuePair<string, string> pair in expected)
            {
                Assert.That(actual.ContainsKey(pair.Key), Is.True, "Missing Runtime AI Lab material " + pair.Key);
                Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void RuntimeAiLabSceneAsset_BindsVfxCapableCharacterTestProfile()
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity");

            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "Runtime AI Lab scene must contain a GameManager.");
            Assert.IsNotNull(
                gameManager.ConfiguredProfile,
                "Runtime AI Lab GameManager must bind the shared CharacterTest scene profile.");
            Assert.IsNotNull(
                gameManager.ConfiguredProfile.FireballVfxPrefab,
                "Runtime AI Lab profile must provide the base VFX resource set.");
            Assert.IsNotNull(
                gameManager.ConfiguredProfile.ChainLightningChargeVfxPrefab,
                "Runtime AI Lab profile must provide the charge VFX used by the stun-bolt binding.");
        }
    }
}
