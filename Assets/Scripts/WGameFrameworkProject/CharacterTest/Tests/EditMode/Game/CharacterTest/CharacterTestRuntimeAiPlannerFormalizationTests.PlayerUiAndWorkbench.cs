using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [Test]
        public void RuntimeAiBehaviorLoadoutPanelViewModel_ListsSelectableUnitsAndStrategies()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 3f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel locked =
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModelBuilder.Build(
                    harness.Services,
                    ai.Id.Value,
                    selectedLoadoutId: 0,
                    selectedVariantId: 0);

            Assert.AreEqual(1, locked.TotalEntityCount);
            Assert.IsTrue(ContainsEntityRow(locked, ai.Id.Value, selected: true));
            Assert.Greater(locked.TotalLoadoutCount, 0);
            Assert.IsTrue(ContainsSelectedLoadoutCard(locked));
            Assert.IsFalse(locked.PhaseUnlocked);
            Assert.IsFalse(locked.CanApply);
            StringAssert.Contains("Behavior order", locked.BehaviorOrderText);
            Assert.IsNotEmpty(locked.ActionChainText);

            harness.Modules.RuntimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(true);
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel unlocked =
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModelBuilder.Build(
                    harness.Services,
                    ai.Id.Value,
                    locked.SelectedLoadoutId,
                    locked.SelectedVariantId);

            Assert.IsTrue(unlocked.PhaseUnlocked);
            Assert.IsTrue(unlocked.CanApply);
        }

        [Test]
        public void RuntimeAiBehaviorLoadoutPanelCommandSink_SubmitsSelectionOnlyWhenPhaseUnlocked()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var sink = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandSink(
                harness.Services);

            Assert.AreEqual(ai.Id.Value, sink.SelectedEntityId);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.ApplySelection,
                payload: null));

            Assert.AreEqual(1, sink.ApplyCount);
            StringAssert.Contains("locked", sink.LastStatusMessage);
            Assert.AreEqual(0, harness.Modules.PendingRuntimeCommandCount);
            Assert.IsFalse(ai.Control.RuntimeAiBehaviorSelection.HasSelection);

            harness.Modules.RuntimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(true);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.ApplySelection,
                payload: null));

            Assert.AreEqual(2, sink.ApplyCount);
            Assert.IsTrue(sink.LastApplyResult.Success, sink.LastApplyResult.Error.Message);
            Assert.Greater(harness.Modules.PendingRuntimeCommandCount, 0);

            harness.Tick();

            Assert.IsTrue(ai.Control.RuntimeAiBehaviorSelection.HasSelection);
            Assert.AreEqual(sink.SelectedLoadoutId, ai.Control.RuntimeAiBehaviorSelection.LoadoutId);
            Assert.AreEqual(sink.SelectedVariantId, ai.Control.RuntimeAiBehaviorSelection.VariantId);
        }

        [Test]
        public void ArenaSetupPanelViewModel_FormatsBattleSidesAsAllyAndEnemy()
        {
            var running = new CharacterTestArenaSnapshot(
                CharacterTestArenaMatchPhase.Running,
                "Arena battle running.",
                12.5f,
                winnerTeamId: 0,
                team1Alive: 2,
                team2Alive: 1,
                team1Total: 3,
                team2Total: 3,
                CharacterTestArenaMatchConfig.CreateDefault(),
                null,
                CharacterTestCameraMode.MultiTargetAuto);
            var team1Win = new CharacterTestArenaSnapshot(
                CharacterTestArenaMatchPhase.Finished,
                "done",
                21.25f,
                CharacterTestArenaMatchConfig.Team1Id,
                team1Alive: 1,
                team2Alive: 0,
                team1Total: 1,
                team2Total: 1,
                CharacterTestArenaMatchConfig.CreateDefault(),
                null,
                CharacterTestCameraMode.MultiTargetAuto);
            var team2Win = new CharacterTestArenaSnapshot(
                CharacterTestArenaMatchPhase.Finished,
                "done",
                31f,
                CharacterTestArenaMatchConfig.Team2Id,
                team1Alive: 0,
                team2Alive: 1,
                team1Total: 1,
                team2Total: 1,
                CharacterTestArenaMatchConfig.CreateDefault(),
                null,
                CharacterTestCameraMode.MultiTargetAuto);

            string battleStatus =
                CharacterTestFairyGuiArenaSetupPanelViewModelBuilder.FormatBattleStatus(running);

            StringAssert.Contains("我方(蓝) 2/3", battleStatus);
            StringAssert.Contains("敌方(红) 1/3", battleStatus);
            StringAssert.Contains(
                "我方胜利",
                CharacterTestFairyGuiArenaSetupPanelViewModelBuilder.FormatResult(team1Win));
            StringAssert.Contains(
                "敌方胜利",
                CharacterTestFairyGuiArenaSetupPanelViewModelBuilder.FormatResult(team2Win));
        }

        [Test]
        public void RuntimeAiPlannerBehaviorSelectionCommand_AppliesSelectionAndEntersSnapshotHash()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 3f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var participant = new CharacterTestNetworkSnapshotParticipant(
                harness.Modules.CharacterRuntime,
                harness.Modules.RuntimeCommands);
            RuntimeFrame hashFrame = harness.CurrentFrame;
            harness.Modules.RuntimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(true);
            long hashBefore = participant.ComputeStateHash(hashFrame);

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetRuntimeAiBehaviorSelection(
                harness.CurrentFrame,
                CharacterTestRuntimeCommandSources.System,
                ai.Id.Value,
                loadoutId: 1,
                variantId: 1);
            RuntimeCommandValidationResult enqueue = harness.Enqueue(command);
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

            harness.Tick();

            CharacterTestRuntimeAiBehaviorSelection selection =
                ai.Control.RuntimeAiBehaviorSelection;
            Assert.IsTrue(selection.HasSelection);
            Assert.AreEqual(1, selection.LoadoutId);
            Assert.AreEqual(1, selection.VariantId);
            Assert.AreEqual(selection.ProfileSlot, ai.Control.RuntimeAiPlannerProfileSlot);
            Assert.AreNotEqual(hashBefore, participant.ComputeStateHash(hashFrame));

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = FindAgentSnapshot(snapshot, ai.Id.Value);
            Assert.AreEqual(selection.LoadoutId, agent.RuntimeAiBehaviorLoadoutId);
            Assert.AreEqual(selection.VariantId, agent.RuntimeAiBehaviorVariantId);
            Assert.AreEqual(selection.CatalogHash, agent.RuntimeAiBehaviorCatalogHash);
            Assert.AreEqual(selection.ProfileSlot, agent.RuntimeAiBehaviorProfileSlot);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorSelectionSnapshot_RejectsCatalogMismatch()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Assert.IsTrue(harness.Modules.RuntimeAiBehaviorLoadouts.TryResolveSelection(
                1,
                1,
                out CharacterTestRuntimeAiBehaviorSelection selection,
                out string message), message);
            var snapshot = new CharacterTestNetworkCharacterSnapshot
            {
                EntityId = 1,
                CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                TeamId = 2,
                DefaultTeamId = 2,
                ControlOwner = CharacterTestCharacterControlOwner.AiControlled,
                RuntimeAiPlannerProfileSlot = selection.ProfileSlot,
                RuntimeAiBehaviorLoadoutId = selection.LoadoutId,
                RuntimeAiBehaviorVariantId = selection.VariantId,
                RuntimeAiBehaviorCatalogHash = selection.CatalogHash + 1L,
                RuntimeAiBehaviorProfileSlot = selection.ProfileSlot
            };

            NetworkSnapshotApplyResult result = harness.Modules.CharacterRuntime.ApplyNetworkCharacters(
                new[] { snapshot },
                harness.CurrentFrame);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("catalog hash mismatch", result.Message);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorSelectionSnapshot_LegacySchemaClearsSelectionFields()
        {
            var snapshot = new CharacterTestNetworkCharacterSnapshot
            {
                RuntimeAiPlannerProfileSlot = 9,
                RuntimeAiBehaviorLoadoutId = 1,
                RuntimeAiBehaviorVariantId = 2,
                RuntimeAiBehaviorCatalogHash = 123L,
                RuntimeAiBehaviorProfileSlot = 9
            };
            var document = new CharacterTestNetworkSnapshotDocument
            {
                SchemaVersion = CharacterTestNetworkSnapshotParticipant.RuneTurretSchemaVersionValue,
                RuntimeAiBehaviorSelectionUnlocked = true,
                Characters = new[] { snapshot }
            };

            document.NormalizeForSchema(document.SchemaVersion);

            Assert.IsFalse(document.RuntimeAiBehaviorSelectionUnlocked);
            Assert.AreEqual(0, snapshot.RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(0, snapshot.RuntimeAiBehaviorLoadoutId);
            Assert.AreEqual(0, snapshot.RuntimeAiBehaviorVariantId);
            Assert.AreEqual(0L, snapshot.RuntimeAiBehaviorCatalogHash);
            Assert.AreEqual(0, snapshot.RuntimeAiBehaviorProfileSlot);
        }

    }
}
