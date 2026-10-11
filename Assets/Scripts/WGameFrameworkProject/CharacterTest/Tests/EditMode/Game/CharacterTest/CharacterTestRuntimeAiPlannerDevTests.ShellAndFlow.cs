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
using Fgui = global::FairyGUI;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabShell_InitialStateOwnsVisiblePlayerDeckWorkspace()
        {
            var host = new GameObject("Runtime AI Lab Player Deck Entry Gate Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                CharacterTestRuntimeAiLabShellCoordinator coordinator =
                    controller.EnsureShellCoordinator(playerDeckAvailable: true);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabShellVisibility.Visible,
                    coordinator.CurrentState.Visibility);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    coordinator.CurrentState.PresentationMode);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                    coordinator.CurrentState.ActiveWorkspace);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabShell_DedicatedSceneStartsInSetupWorkspace()
        {
            var host = new GameObject("Runtime AI Lab Setup Entry Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                CharacterTestRuntimeAiLabShellCoordinator coordinator =
                    controller.EnsureShellCoordinator(
                        playerDeckAvailable: true,
                        labAvailable: true,
                        initialWorkspace: CharacterTestRuntimeAiLabWorkspace.Setup);

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabShellVisibility.Visible,
                    coordinator.CurrentState.Visibility);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    coordinator.CurrentState.PresentationMode);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspace.Setup,
                    coordinator.CurrentState.ActiveWorkspace);
                Assert.AreEqual("INITIAL_SETUP", coordinator.LastNavigationResult.Code);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabShell_StartFailsClosedWhenDraftWorkspaceIsUnavailable()
        {
            var host = new GameObject("Runtime AI Lab Missing Draft Start Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.EnsureShellCoordinator(
                    playerDeckAvailable: true,
                    labAvailable: true,
                    initialWorkspace: CharacterTestRuntimeAiLabWorkspace.Setup);

                bool started = false;
                Assert.DoesNotThrow(() => started = controller.StartSessionThroughShell());
                Assert.IsFalse(started);
                StringAssert.Contains("draft workspace is unavailable", controller.StatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabController_SetupSpawnEditsDriveSpawnSpecs()
        {
            var host = new GameObject("Runtime AI Lab Spawn Edit Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
                Assert.IsTrue(controller.SetSelectedSetupSpawnPosition(new Vector2(-18.2f, 12.4f)));
                Assert.IsTrue(controller.MirrorSetupSpawns());
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                    controller.MatchConfig.BehaviorGroups.CreateRuntime(
                        CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                        null,
                        NullRuntimeLogger.Instance);

                bool built = controller.MatchConfig.TryBuildSpawnSpecs(
                    runtime,
                    out CharacterTestCharacterSpawnSpec[] specs,
                    out string message);

                Assert.IsTrue(built, message);
                Assert.AreEqual(-18f, specs[0].X, 0.001f);
                Assert.AreEqual(12f, specs[0].Z, 0.001f);
                Assert.AreEqual(18f, specs[1].X, 0.001f);
                Assert.AreEqual(12f, specs[1].Z, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSpawnValidator_BlocksInvalidStartsAndKeepsHazardAsWarning()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();

            config.SetSpawnPosition(CharacterTestArenaMatchConfig.Team1Id, 0, new Vector2(17f, 14f));
            CharacterTestRuntimeAiLabSpawnValidationResult obstacle =
                CharacterTestRuntimeAiLabSpawnValidator.Validate(config);

            Assert.IsFalse(obstacle.CanStart);
            Assert.IsTrue(obstacle.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabSpawnSlotValidation slot));
            Assert.AreEqual(CharacterTestRuntimeAiLabSpawnSeverity.Error, slot.Severity);

            config.SetSpawnPosition(CharacterTestArenaMatchConfig.Team1Id, 0, new Vector2(-17f, -14f));
            CharacterTestRuntimeAiLabSpawnValidationResult hazard =
                CharacterTestRuntimeAiLabSpawnValidator.Validate(config);

            Assert.IsTrue(hazard.CanStart);
            Assert.IsTrue(hazard.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out slot));
            Assert.AreEqual(CharacterTestRuntimeAiLabSpawnSeverity.Warning, slot.Severity);
        }

        [Test]
        public void RuntimeAiLabController_MatchConfigBuildsSpawnSpecsAtLabPads()
        {
            var host = new GameObject("Runtime AI Lab Spawn Config Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                    controller.MatchConfig.BehaviorGroups.CreateRuntime(
                        CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                        null,
                        NullRuntimeLogger.Instance);

                bool built = controller.MatchConfig.TryBuildSpawnSpecs(
                    runtime,
                    out CharacterTestCharacterSpawnSpec[] specs,
                    out string message);

                Assert.IsTrue(built, message);
                Assert.AreEqual(2, specs.Length);
                Assert.AreEqual(CharacterTestArenaMatchConfig.Team1Id, specs[0].TeamId);
                Assert.AreEqual(-27f, specs[0].X, 0.001f);
                Assert.AreEqual(1f, specs[0].Y, 0.001f);
                Assert.AreEqual(14f, specs[0].Z, 0.001f);
                Assert.AreEqual(CharacterTestArenaMatchConfig.Team2Id, specs[1].TeamId);
                Assert.AreEqual(27f, specs[1].X, 0.001f);
                Assert.AreEqual(1f, specs[1].Y, 0.001f);
                Assert.AreEqual(14f, specs[1].Z, 0.001f);
                Assert.IsTrue(specs[0].RuntimeAiBehaviorSelection.HasSelection);
                Assert.IsTrue(specs[1].RuntimeAiBehaviorSelection.HasSelection);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_StartSessionSpawnsTestCharacterViews()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            controller?.PrepareInitialSetupState(restorePersistedDraft: false);
            int setupSpawnedCount = manager?.RuntimeContext?.Slice?.CharacterEntities?.Count ?? 0;
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                new Vector2(-22f, 12f)));
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team2Id,
                0,
                new Vector2(22f, 12f)));
            bool started = controller != null && controller.StartSession();
            string status = controller != null ? controller.StatusMessage : "Runtime AI Lab scene controller not found.";
            bool startedPaused = manager?.RuntimeContext?.Paused ?? false;
            CharacterTestRuntimeAiPlannerDevFrameSnapshot firstFrame = null;
            bool capturedAgents = controller != null
                && controller.FrameBuffer.TryGetSelected(out firstFrame)
                && firstFrame.Agents.Count >= 2;
            bool selectedSecondAgentFromScene = false;
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (capturedAgents && mainCamera != null)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot secondAgent = firstFrame.Agents[1];
                Vector3 secondAgentScreenPosition = mainCamera.WorldToScreenPoint(new Vector3(
                    secondAgent.ScenePreviewSourceCenterX,
                    secondAgent.ScenePreviewSourceCenterY,
                    secondAgent.ScenePreviewSourceCenterZ));
                selectedSecondAgentFromScene = secondAgentScreenPosition.z > 0f
                    && controller.TrySelectAgentByScreenPosition(
                        mainCamera,
                        new Vector2(secondAgentScreenPosition.x, secondAgentScreenPosition.y))
                    && controller.SelectedAgentIndex == 1;
            }
            int spawnedCount = 0;
            int localPlayerCount = 0;
            float lowestCharacterY = float.PositiveInfinity;
            bool allCharactersGrounded = false;
            bool team1SpawnedAtEditedPosition = false;
            bool team2SpawnedAtEditedPosition = false;
            if (started)
            {
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                    spawnedCount = manager?.RuntimeContext?.Slice?.CharacterEntities?.Count ?? 0;
                    if (spawnedCount >= 2)
                        break;
                }

                IReadOnlyList<CharacterTestCharacterEntity> entities =
                    manager?.RuntimeContext?.Slice?.CharacterEntities;
                if (entities != null)
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        CharacterTestCharacterEntity entity = entities[i];
                        if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                            localPlayerCount++;
                        if (entity.Team.TeamId == CharacterTestArenaMatchConfig.Team1Id)
                        {
                            team1SpawnedAtEditedPosition |= Mathf.Abs(entity.Transform.X + 22f) < 2f
                                && Mathf.Abs(entity.Transform.Z - 12f) < 2f;
                        }
                        else if (entity.Team.TeamId == CharacterTestArenaMatchConfig.Team2Id)
                        {
                            team2SpawnedAtEditedPosition |= Mathf.Abs(entity.Transform.X - 22f) < 2f
                                && Mathf.Abs(entity.Transform.Z - 12f) < 2f;
                        }
                    }
                }

                for (int i = 0; i < 120; i++)
                    manager.StepSimulationOnceForRuntimeAiPlannerDev();

                entities = manager?.RuntimeContext?.Slice?.CharacterEntities;
                spawnedCount = entities?.Count ?? 0;
                localPlayerCount = 0;
                allCharactersGrounded = entities != null && entities.Count >= 2;
                if (entities != null)
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        CharacterTestCharacterEntity entity = entities[i];
                        if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                            localPlayerCount++;
                        if (entity.Transform.Y < lowestCharacterY)
                            lowestCharacterY = entity.Transform.Y;
                        allCharactersGrounded &= entity.IsGrounded;
                    }
                }
            }

            yield return new ExitPlayMode();

            Assert.IsNotNull(manager, "Runtime AI Lab scene GameManager must exist.");
            Assert.AreEqual(
                0,
                setupSpawnedCount,
                "Runtime AI Lab setup should only show setup preview slots; real runtime characters spawn after Start.");
            Assert.IsTrue(started, status);
            Assert.IsTrue(startedPaused, "Runtime AI Lab should pause at the first captured frame after Start.");
            Assert.IsTrue(
                capturedAgents,
                "Runtime AI Lab should capture the first frame only after all enabled Runtime AI Planner agents exist.");
            Assert.IsTrue(
                selectedSecondAgentFromScene,
                "Runtime AI Lab should select an Agent through the screen-space 3D scene selection path.");
            Assert.AreEqual(
                2,
                spawnedCount,
                "Runtime AI Lab StartSession should spawn exactly the two enabled setup slots.");
            Assert.AreEqual(
                0,
                localPlayerCount,
                "Runtime AI Lab peer is an unbound experiment operator and must not create a LocalPlayer character.");
            Assert.IsTrue(
                team1SpawnedAtEditedPosition,
                "Runtime AI Lab StartSession should use the edited Team 1 setup spawn position.");
            Assert.IsTrue(
                team2SpawnedAtEditedPosition,
                "Runtime AI Lab StartSession should use the edited Team 2 setup spawn position.");
            Assert.GreaterOrEqual(
                lowestCharacterY,
                -0.05f,
                "Runtime AI Lab characters should stay on the generated runtime ground instead of falling through.");
            Assert.IsTrue(
                allCharactersGrounded,
                "Runtime AI Lab characters should resolve grounded after the generated runtime ground is added to physics.");
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_PrimaryStartDefaultsToBehaviorLogic()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            Assert.IsNotNull(panelHost, "Runtime AI Lab panel host must exist.");

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                controller.ShellCoordinator;
            Assert.IsNotNull(coordinator, "Runtime AI Lab shell coordinator must exist.");

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase,
                panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                coordinator.CurrentState.PageContext.TimelineMode,
                "The primary Start command should initially open Behavior Logic.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode,
                "The rebuilt runtime panel must synchronize the shell's initial mode.");

            yield return null;

            panelHost.Refresh();
            Assert.IsNotNull(panelHost.LastViewModel);
            Assert.IsTrue(panelHost.LastViewModel.ShowBehaviorLogic);
            Assert.IsTrue(panelHost.LastViewModel.BehaviorLogicModeSelected);
            Assert.IsFalse(panelHost.LastViewModel.RuntimeTraceModeSelected);
            Assert.IsFalse(panelHost.LastViewModel.ShowTimeline);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_SaveAndRunKeepsBehaviorLogicPanelMounted()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(panelHost);

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));
            Assert.IsTrue(panelHost.IsOpen, panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);

            int selectedEntry = panelHost.CommandSink.SelectedWorkbenchEntryIndex;
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup));
            CharacterTestRuntimeAiLabBehaviorCardDraft selectedCard =
                controller.DraftWorkspace.Cards[selectedGroup.Entries[selectedEntry].CardDraftId];
            var entryIdsBeforeDuplicate = new HashSet<string>(
                selectedGroup.Entries.Select(entry => entry.EntryId),
                StringComparer.Ordinal);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload edited =
                selectedCard.LogicPayload.Clone();
            edited.RiskSummary += " 面板保存测试";
            Assert.IsTrue(controller.DraftWorkspace.TryReplaceCardLogicPayload(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                selectedEntry,
                edited,
                out string editMessage), editMessage);
            Assert.IsTrue(
                controller.TryDuplicateDraftEntry(selectedEntry, out string duplicateMessage),
                duplicateMessage);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out selectedGroup));
            int duplicateEntryIndex = -1;
            for (int entryIndex = 0; entryIndex < selectedGroup.Entries.Count; entryIndex++)
            {
                if (!entryIdsBeforeDuplicate.Contains(selectedGroup.Entries[entryIndex].EntryId))
                {
                    duplicateEntryIndex = entryIndex;
                    break;
                }
            }
            Assert.GreaterOrEqual(duplicateEntryIndex, 0, "The duplicated entry must have a stable new identity.");
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft,
                null));

            int remountBudget = 10;
            while ((controller.BehaviorRecipeSaveQueued || !panelHost.IsOpen) && remountBudget-- > 0)
                yield return null;

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase,
                panelHost.CommandSink.LastStatusMessage);
            Assert.IsTrue(
                controller.CaptureSnapshot().Paused,
                "保存前暂停时，保存后的重算结果也应保持暂停。");
            Assert.IsTrue(
                panelHost.IsOpen,
                "Save and Run must re-mount the scene-bound Runtime AI Lab panel after rebuilding the runtime session. "
                + "configured=" + panelHost.IsConfigured
                + " shell=" + controller.ShellCoordinator?.CurrentState.ActiveWorkspace
                + "/" + controller.ShellCoordinator?.CurrentState.PresentationMode
                + "/" + controller.ShellCoordinator?.CurrentState.Visibility
                + " status=" + panelHost.CommandSink?.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);
            Assert.AreEqual(selectedEntry, panelHost.CommandSink.SelectedWorkbenchEntryIndex);
            panelHost.Refresh();
            Assert.AreEqual(
                controller.StatusMessage,
                panelHost.CommandSink.LastStatusMessage,
                "The deferred Save command must replace its temporary progress text with the final controller result.");
            Assert.IsNotNull(panelHost.LastViewModel);
            Assert.IsTrue(panelHost.LastViewModel.ShowBehaviorLogic);
            Assert.IsTrue(panelHost.IsOpen);

            string duplicateCardDraftId = selectedGroup.Entries[duplicateEntryIndex].CardDraftId;
            panelHost.CommandSink.SetWorkbenchEntryIndex(duplicateEntryIndex);
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                duplicateEntryIndex));
            Assert.AreNotEqual(
                duplicateCardDraftId,
                controller.ShellCoordinator.CurrentState.PageContext.CardDraftId,
                "Deleting the selected card must replace the shell's stale card identity before saving.");
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft,
                null));

            int deleteSaveBudget = 10;
            while ((controller.BehaviorRecipeSaveQueued || !panelHost.IsOpen) && deleteSaveBudget-- > 0)
                yield return null;

            Assert.IsFalse(controller.BehaviorRecipeSaveQueued);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevScenePhase.Running, controller.Phase);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedAndRunning,
                controller.LastBehaviorRecipeSaveRun.Status,
                controller.LastBehaviorRecipeSaveRun.Message);
            Assert.IsTrue(panelHost.IsOpen, "Deleting a Behavior Card and saving must keep the Lab UI mounted.");
            Assert.IsTrue(
                panelHost.IsActuallyVisible,
                "The panel registry, FairyGUI root visibility, and Stage attachment must all remain valid.");
            Assert.IsNotNull(panelHost.RootForTests);
            Assert.IsTrue(panelHost.RootForTests.visible);
            Assert.IsTrue(panelHost.RootForTests.onStage);
            Assert.IsNotNull(panelHost.RootForTests.parent);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                controller.ShellCoordinator.CurrentState.Visibility);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                controller.ShellCoordinator.CurrentState.PresentationMode);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);
            panelHost.Refresh();
            Assert.IsNotNull(panelHost.LastViewModel);
            Assert.IsTrue(panelHost.LastViewModel.ShowBehaviorLogic);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_BlankGroupPrimaryStartEntersRunning()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(panelHost);

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase,
                panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual("测试已开始。", panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                controller.ShellCoordinator.CurrentState.ActiveWorkspace);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace,
                null));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                panelHost.CommandSink.TimelineMode);

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic,
                null));
            Assert.AreEqual(
                "已切换到编辑逻辑。",
                panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                controller.ShellCoordinator.CurrentState.PageContext.TimelineMode);
            panelHost.Refresh();
            Assert.IsNotNull(panelHost.LastViewModel);
            Assert.IsTrue(panelHost.LastViewModel.ShowBehaviorLogic);
            Assert.IsTrue(
                panelHost.LastViewModel.CanEditWorkbench,
                "A blank editable group must expose the Behavior Logic add-card affordance.");
            Fgui.GButton addBehaviorCard =
                panelHost.RootForTests.GetChild("btnWorkbenchEdit")?.asButton;
            Assert.IsNotNull(addBehaviorCard);
            Assert.IsTrue(addBehaviorCard.visible);
            Assert.IsTrue(addBehaviorCard.enabled);
            Assert.AreEqual("+ 添加行为卡", addBehaviorCard.title);
            Fgui.GButton createBehaviorCard = panelHost.RootForTests.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard)?.asButton;
            Assert.IsNotNull(createBehaviorCard);
            Assert.IsTrue(createBehaviorCard.visible);
            Assert.IsTrue(createBehaviorCard.enabled);
            Assert.AreEqual("新建卡", createBehaviorCard.title);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineRootX
                    + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicCommandRailButtonX,
                createBehaviorCard.x,
                0.1f);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY
                    + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicCommandRailCreateCardY,
                createBehaviorCard.y,
                0.1f);
            Fgui.GButton deleteBehaviorCard = panelHost.RootForTests.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry)?.asButton;
            Assert.IsNotNull(deleteBehaviorCard);
            Assert.IsFalse(deleteBehaviorCard.visible);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_PrimaryStartPausesArenaAndOpensRuntimeFrame()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            Assert.IsNotNull(manager, "Runtime AI Lab GameManager must exist.");
            Assert.IsNotNull(panelHost, "Runtime AI Lab panel host must exist.");

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                controller.ShellCoordinator;
            Assert.IsNotNull(coordinator, "Runtime AI Lab shell coordinator must exist.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                coordinator.CurrentState.Visibility);

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase,
                panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual("测试已开始。", panelHost.CommandSink.LastStatusMessage);
            Assert.IsTrue(
                manager.RuntimeContext.Paused,
                "The primary Start command should pause the arena after capturing its baseline.");
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevTimelineMode.Paused,
                controller.Timeline.Mode);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                coordinator.CurrentState.Visibility,
                "The Runtime AI Lab shell should remain visible after a successful primary start.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace,
                "The primary Start command should open RuntimeFrame diagnostics.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded,
                coordinator.CurrentState.LastCompletedOutcome);
            Assert.Greater(controller.FrameBuffer.Count, 0);

            yield return null;

            panelHost.Refresh();

            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel runtimeFrame =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    panelHost.CommandSink.LastStatusMessage,
                    workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                    diagnosticsTab: CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                    timelineMode: panelHost.CommandSink.TimelineMode).RuntimeFrame;
            Assert.IsNotNull(runtimeFrame, "Runtime-frame projection must be available after Start.");
            Assert.AreEqual(2, runtimeFrame.AgentOptions.Length);
            CollectionAssert.AreEquivalent(
                new[] { "T1-1", "T2-1" },
                new[]
                {
                    runtimeFrame.AgentOptions[0].SlotLabel,
                    runtimeFrame.AgentOptions[1].SlotLabel
                });
            CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel selectedOption =
                runtimeFrame.AgentOptions[controller.SelectedAgentIndex];
            Assert.IsTrue(selectedOption.Selected);
            Assert.AreEqual("T1-1", selectedOption.SlotLabel);
            Assert.AreEqual(
                selectedOption.EntityId,
                runtimeFrame.AgentEntityId);

            int otherAgentIndex = controller.SelectedAgentIndex == 0 ? 1 : 0;
            for (int frame = 0; frame < 16 && controller.SelectedAgentIndex != otherAgentIndex; frame++)
            {
                panelHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RuntimeTestSelectAgent,
                    otherAgentIndex));
                yield return null;
            }

            Assert.AreEqual(otherAgentIndex, controller.SelectedAgentIndex);
            runtimeFrame = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                controller,
                panelHost.CommandSink.LastStatusMessage,
                workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                diagnosticsTab: CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                timelineMode: panelHost.CommandSink.TimelineMode).RuntimeFrame;
            Assert.IsTrue(
                runtimeFrame.AgentOptions[otherAgentIndex].Selected);
            Assert.AreEqual(
                runtimeFrame.AgentOptions[otherAgentIndex].EntityId,
                runtimeFrame.AgentEntityId);

            int previousSessionGeneration = controller.Timeline.SessionIdentity.Generation;
            int frameBeforeStep = controller.Timeline.LatestFrame;
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step,
                null));
            Assert.Greater(controller.Timeline.LatestFrame, frameBeforeStep);

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan,
                null));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                panelHost.CommandSink.TimelineMode);

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                null));

            Assert.AreEqual(
                "测试已重新开始，并停在首个可见帧。",
                panelHost.CommandSink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                coordinator.CurrentState.PageContext.TimelineMode,
                "A migrated published group keeps its Behavior Plan context after restart.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                panelHost.CommandSink.TimelineMode);
            Assert.Greater(
                controller.Timeline.SessionIdentity.Generation,
                previousSessionGeneration);
            Assert.AreEqual(
                controller.Timeline.SessionIdentity.SessionStartFrame,
                controller.Timeline.LatestFrame);
            Assert.AreEqual(
                controller.Timeline.LatestFrame,
                controller.Timeline.SelectedFrame);
            runtimeFrame = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                controller,
                panelHost.CommandSink.LastStatusMessage,
                workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                diagnosticsTab: CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                timelineMode: panelHost.CommandSink.TimelineMode).RuntimeFrame;
            Assert.IsNotNull(runtimeFrame, "Runtime-frame projection must survive Reset.");
            selectedOption = runtimeFrame.AgentOptions[controller.SelectedAgentIndex];
            Assert.IsTrue(selectedOption.Selected);
            Assert.AreEqual(
                "T1-1",
                selectedOption.SlotLabel,
                "Restart must restore the configured baseline slot even when runtime Agent ordering differs.");
            Assert.IsTrue(manager.RuntimeContext.Paused);

            Assert.IsTrue(
                manager.RuntimeContext.Paused,
                "Opening RuntimeFrame diagnostics must preserve the paused arena state.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_LiveBehaviorLogicDeletesBehaviorCard()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(panelHost);

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(manager);
            Assert.IsFalse(manager.RuntimeContext.Paused);
            Assert.IsTrue(controller.CanEditBehaviorCardDraft, controller.StatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                panelHost.CommandSink.TimelineMode);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft workspaceGroup));
            int countBeforeDelete = workspaceGroup.Entries.Count;
            int entryIndex = -1;
            for (int i = 0; i < workspaceGroup.Entries.Count; i++)
            {
                if (controller.DraftWorkspace.Cards.TryGetValue(
                        workspaceGroup.Entries[i].CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    && string.Equals(
                        card.PublishedStableId,
                        CharacterTestRuntimeAiLabBehaviorRecipeV5Templates
                            .FoundationApproachAndAttackStableCardId,
                        StringComparison.Ordinal))
                {
                    entryIndex = i;
                    break;
                }
            }
            Assert.GreaterOrEqual(entryIndex, 0, "持续接敌必须存在于默认测试行为组中。");

            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                entryIndex));

            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out workspaceGroup));
            Assert.AreEqual(countBeforeDelete - 1, workspaceGroup.Entries.Count);
            StringAssert.Contains("已从当前行为组移除", panelHost.CommandSink.LastStatusMessage);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_BehaviorLogicCanSelectDifferentRecipeEventsConsecutively()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(panelHost);

            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            panelHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));
            yield return null;
            panelHost.Refresh();

            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                controller.ShellCoordinator.CurrentState.ActiveWorkspace);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                controller.ShellCoordinator.CurrentState.PageContext.TimelineMode);

            Fgui.GComponent root = panelHost.RootForTests;
            Fgui.GComponent rows = root
                ?.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)
                ?.asCom
                ?.GetChild("timelineGroupHost")
                ?.asCom
                ?.GetChild("recipeViewport")
                ?.asCom
                ?.GetChild("recipeRows")
                ?.asCom;
            Assert.IsNotNull(rows);
            Fgui.GComponent firstBranch = rows.GetChild("recipeTrack1")?.asCom
                ?.GetChild("trackContent")?.asCom
                ?.GetChild("eventSpan")?.asCom;
            Fgui.GComponent secondBranch = rows.GetChild("recipeTrack2")?.asCom
                ?.GetChild("trackContent")?.asCom
                ?.GetChild("eventSpan")?.asCom;
            Assert.IsNotNull(firstBranch);
            Assert.IsNotNull(secondBranch);

            firstBranch.onClick.Call();
            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                panelHost.CommandSink.SelectedLogicElementKind);
            int firstDetectorIndex = panelHost.CommandSink.SelectedLogicElementIndex;

            secondBranch.onClick.Call();
            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                panelHost.CommandSink.SelectedLogicElementKind);
            Assert.AreNotEqual(
                firstDetectorIndex,
                panelHost.CommandSink.SelectedLogicElementIndex,
                "Selecting the next Event must replace the previous Detector selection.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_AdvanceFrameIsAvailableOnlyAtLatest()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            bool started = false;
            int runtimeReadyBudget = 30;
            while (!started && runtimeReadyBudget-- > 0)
            {
                started = controller.StartSession();
                if (!started)
                    yield return null;
            }
            Assert.IsTrue(started, controller.StatusMessage);
            yield return null;

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel latestViewModel =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null);
            Assert.AreEqual("推进 +1", latestViewModel.StepLabel);
            Assert.IsTrue(latestViewModel.CanStep);
            int latestBeforeAdvance = controller.Timeline.LatestFrame;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(panelHost);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink = panelHost.CommandSink;
            Assert.IsNotNull(sink);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step,
                null));
            Assert.AreEqual(latestBeforeAdvance + 1, controller.Timeline.LatestFrame);
            Assert.AreEqual(controller.Timeline.LatestFrame, controller.Timeline.SelectedFrame);
            int newestFrame = controller.Timeline.LatestFrame;
            StringAssert.Contains("模拟已推进", sink.LastStatusMessage);

            Assert.IsTrue(controller.StepBackFrame(), controller.StatusMessage);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel reviewViewModel =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null);
            Assert.Less(controller.Timeline.SelectedFrame, controller.Timeline.LatestFrame);
            Assert.IsFalse(reviewViewModel.CanStep);

            Assert.IsTrue(controller.SelectLatestFrame(), controller.StatusMessage);
            Assert.AreEqual(newestFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(newestFrame, controller.Timeline.SelectedFrame);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel returnedLatestViewModel =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null);
            Assert.IsTrue(returnedLatestViewModel.CanStep);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_DefaultSessionCanFreezeAndValidateReplayEvidence()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            for (int frame = 0; frame < 10; frame++)
                yield return null;
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;
            Assert.IsTrue(
                controller.CanFreezeReplayValidationCapture,
                "The default Runtime AI Lab Play flow must create a replay-capable loopback session.");
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            Assert.IsTrue(
                controller.FreezeReplayValidationCapture(out string freezeDiagnostic),
                freezeDiagnostic);

            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot ready =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            Assert.AreEqual(CharacterTestReplayValidationState.Ready, ready.State);
            Assert.IsNotNull(ready.TimelineProjection, ready.ResultEvidence);
            CharacterTestRuntimeAiLabReplayReturnToken returnToken =
                controller.CaptureReplayReturnToken();
            Assert.IsTrue(returnToken.IsValid);
            Assert.IsTrue(controller.MatchesReplayReturnToken(returnToken));
            Assert.IsFalse(ready.CanLocateDivergence);
            Assert.IsFalse(controller.TryResolveReplayEvidenceLocator(
                CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Divergence,
                out _,
                out string divergenceAbsence));
            Assert.AreEqual("REPLAY_DIVERGENCE_NOT_CAPTURED", divergenceAbsence);
            AssertReplayEvidenceAvailability(
                controller,
                CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                ready.CanOpenFacts);
            AssertReplayEvidenceAvailability(
                controller,
                CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard,
                ready.CanOpenBehaviorCard);
            if (ready.CanOpenFacts)
            {
                Assert.Greater(controller.ReplayFactsCausalSnapshotCount, 1);
                Assert.IsTrue(controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator currentFacts,
                    out string factsAbsence), factsAbsence);
                CharacterTestRuntimeAiLabReplayCausalSnapshot firstCausal = null;
                CharacterTestRuntimeAiLabReplayCausalSnapshot differentCausal = null;
                for (int frame = ready.StartFrame; frame <= ready.EndFrame; frame++)
                {
                    if (!controller.TryGetReplayFactsCausalSnapshot(
                        frame,
                        currentFacts.AgentEntityId,
                        out CharacterTestRuntimeAiLabReplayCausalSnapshot candidate))
                    {
                        continue;
                    }
                    if (firstCausal == null)
                        firstCausal = candidate;
                    else if (candidate.ReplayFrame != firstCausal.ReplayFrame
                        && candidate.EvidenceContentHash != firstCausal.EvidenceContentHash)
                    {
                        differentCausal = candidate;
                        break;
                    }
                }
                Assert.IsNotNull(firstCausal);
                Assert.IsNotNull(differentCausal, "Two Replay frames must retain independently hashed causal data.");
                Assert.AreNotEqual(firstCausal.ReplayFrame, differentCausal.ReplayFrame);
                Assert.AreNotEqual(firstCausal.EvidenceContentHash, differentCausal.EvidenceContentHash);

                Assert.IsTrue(controller.SelectReplayFrame(firstCausal.ReplayFrame));
                Assert.IsTrue(controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator firstLocator,
                    out factsAbsence), factsAbsence);
                Assert.AreEqual(firstCausal.ReplayFrame, firstLocator.ReplayFrame);
                Assert.IsTrue(controller.SelectReplayFrame(differentCausal.ReplayFrame));
                Assert.IsTrue(controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator secondLocator,
                    out factsAbsence), factsAbsence);
                Assert.AreEqual(differentCausal.ReplayFrame, secondLocator.ReplayFrame);
                Assert.AreNotEqual(firstLocator.ReplayFrame, secondLocator.ReplayFrame);

                var tamperedFacts = new CharacterTestRuntimeAiLabReplayEvidenceLocator(
                    currentFacts.TargetKind,
                    currentFacts.CaptureGeneration,
                    currentFacts.AuthorityRecordHash,
                    currentFacts.TimelineEvidenceHash + 1L,
                    currentFacts.ReplayFrame,
                    currentFacts.LiveFrame,
                    currentFacts.ReplayItem,
                    currentFacts.LiveItem,
                    currentFacts.AgentEntityId,
                    currentFacts.BehaviorLoadoutId,
                    currentFacts.BehaviorEntryId,
                    currentFacts.BehaviorElementKind,
                    currentFacts.BehaviorElementStableId);
                Assert.IsFalse(controller.TryActivateReplayFactsCausalSnapshot(
                    tamperedFacts,
                    out string tamperedAbsence));
                Assert.AreEqual("REPLAY_CAUSAL_EVIDENCE_NOT_CAPTURED", tamperedAbsence);
            }
            Assert.IsTrue(controller.ZoomReplayTimeline(1));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineViewportMode.Manual,
                controller.CaptureReplayValidationWorkspaceSnapshot()
                    .TimelineProjection.Viewport.Mode);
            Assert.IsTrue(controller.PanReplayTimeline(-1));
            Assert.IsTrue(controller.FitReplayTimeline());

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab,
                null));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                sink.DiagnosticsTab,
                sink.LastStatusMessage);

            Assert.IsTrue(
                controller.StartReplayValidation(out string validationDiagnostic),
                validationDiagnostic);
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot busy =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            Assert.IsTrue(busy.IsBusy);
            Assert.IsFalse(busy.CanLocateDivergence);
            Assert.IsFalse(busy.CanOpenFacts);
            Assert.IsFalse(busy.CanOpenBehaviorCard);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel busyView =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    "stale shell status",
                    workspaceMode: sink.WorkspaceMode,
                    diagnosticsTab: sink.DiagnosticsTab);
            Assert.IsFalse(busyView.WorkspaceNavigationEnabled);
            Assert.IsFalse(busyView.Replay.CanReturn);
            StringAssert.StartsWith("正在校验回放 · ", busyView.StatusText);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab,
                null));
            Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Replay, sink.DiagnosticsTab);
            StringAssert.Contains("离开当前工作区前请先取消", sink.LastStatusMessage);
            int timeout = 120;
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot result =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            while (result.IsBusy && timeout-- > 0)
            {
                yield return null;
                result = controller.CaptureReplayValidationWorkspaceSnapshot();
            }

            Assert.AreEqual(
                CharacterTestReplayValidationState.Passed,
                result.State,
                result.Summary + "\n" + result.ResultEvidence);
            Assert.IsFalse(
                controller.ReplayValidationKernelActive,
                "Terminal Replay validation must dispose its isolated playback kernel.");

            Assert.AreEqual(
                CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                sink.DiagnosticsTab);
            Assert.IsTrue(TryFindReplayNavigableItem(
                controller.CaptureReplayValidationWorkspaceSnapshot().TimelineProjection,
                controller,
                out CharacterTestRuntimeAiLabTimelineItem replayItem));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayNavigateItem,
                new CharacterTestRuntimeAiLabTimelineNavigationCommand(
                    replayItem.CreateLocator(),
                    useEndFrame: false)));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                sink.DiagnosticsTab,
                sink.LastStatusMessage);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack,
                null));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                sink.DiagnosticsTab,
                sink.LastStatusMessage);
            Assert.IsTrue(controller.MatchesReplayReturnToken(returnToken));

            if (ready.CanOpenFacts)
            {
                Assert.IsTrue(controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator factsLocator,
                    out string factsAbsence), factsAbsence);
                Assert.IsTrue(
                    controller.TryActivateReplayFactsCausalSnapshot(factsLocator, out factsAbsence),
                    factsAbsence);
                Assert.IsTrue(controller.TryGetActiveReplayFactsCausalSnapshot(
                    out CharacterTestRuntimeAiLabReplayCausalSnapshot causal));
                Assert.IsTrue(causal.TryGetTrace(out CharacterTestRuntimeAiLabDecisionTraceAgentView frozenTrace));
                Assert.Greater(frozenTrace.Facts.Count, 0);
                Assert.Greater(frozenTrace.Candidates.Count, 0);
                CharacterTestRuntimeAiLabCommitTrace frozenCommit = frozenTrace.Commit;

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Facts, sink.DiagnosticsTab);

                controller.Timeline.Clear();
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel frozenFactsView =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        sink.LastStatusMessage,
                        workspaceMode: sink.WorkspaceMode,
                        diagnosticsTab: sink.DiagnosticsTab);
                Assert.IsTrue(frozenFactsView.Facts.Available);
                Assert.AreEqual("Replay Frozen", frozenFactsView.Facts.HistoryStateText);
                Assert.AreEqual(frozenTrace.Facts.Count, frozenFactsView.Facts.FactRows.Length);
                Assert.AreEqual(causal.ReplayFrame, frozenFactsView.Facts.Frame);
                Assert.IsTrue(causal.TryGetTrace(out CharacterTestRuntimeAiLabDecisionTraceAgentView afterEviction));
                Assert.AreEqual(frozenCommit.CardStableId, afterEviction.Commit.CardStableId);
                Assert.AreEqual(frozenCommit.StageStableId, afterEviction.Commit.StageStableId);
                Assert.AreEqual(frozenCommit.TransitionStableId, afterEviction.Commit.TransitionStableId);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Replay, sink.DiagnosticsTab);
            }

            yield return new ExitPlayMode();
        }
    }
}
