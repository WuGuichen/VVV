using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
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
        public void RuntimeCommandModule_MapsCharacterActionExecutionSources()
        {
            Assert.AreEqual(
                CharacterActionSourceKind.LocalInput,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(
                    CharacterTestRuntimeCommandSources.PlayerInput));
            Assert.AreEqual(
                CharacterActionSourceKind.LocalInput,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(
                    CharacterTestRuntimeCommandSources.SkillPanel));
            Assert.AreEqual(
                CharacterActionSourceKind.RuntimeAiPlanner,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(
                    CharacterTestRuntimeCommandSources.RuntimeAiPlanner));
            Assert.AreEqual(
                CharacterActionSourceKind.Scripted,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(
                    CharacterTestRuntimeCommandSources.StoryFlow));
            Assert.AreEqual(
                CharacterActionSourceKind.Debug,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(
                    CharacterTestRuntimeCommandSources.DebugUi));
            Assert.AreEqual(
                CharacterActionSourceKind.Command,
                CharacterTestRuntimeCommandModule.ResolveCharacterActionSource(int.MaxValue));
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_SubmitsRuntimeAiPlannerControlIntent()
        {
            using var harness = CreateHarness(
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 4f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            // M1 Planner PreSimulation is authority-bound to the previous
            // completed frame. The spawn tick only creates the AI entity; the
            // next tick is the first one with a complete source/target frame.
            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, command.CommandId);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.RuntimeAiPlanner, command.SourceId);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.AreEqual(ai.Id.Value, intent.TargetEntityId);
            Assert.Less(intent.MoveXMilli, 0);
            Assert.IsFalse(intent.AttackPrimaryPressed);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_MovingAiTurnsYawTowardMoveIntent()
        {
            using var harness = CreateHarness(
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 4f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            // M1 Planner PreSimulation is authority-bound to the previous
            // completed frame. The spawn tick only creates the AI entity; the
            // next tick is the first one with a complete source/target frame.
            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.Less(intent.WorldMoveXMilli, 0);
            Assert.Less(ai.Transform.X, 4f);
            Assert.AreNotEqual(0, ai.Transform.YawMilliDegrees);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_DoesNotSubmitWhenNoTargetWasPreviouslyWritten()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Agents.Count);
            Assert.AreEqual("TargetMissing", snapshot.Agents[0].SuppressedReason);
            Assert.AreEqual("none", snapshot.Agents[0].LastCommand);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_SkillCasterSubmitsAbilityHotbarCommand()
        {
            using var harness = CreateHarness(
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateSkillCasterTemplate(),
                disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
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

            CharacterTestAbilitySlotCastRequest request = default;
            bool foundAbilityCast = false;
            for (int i = 0; i < 3; i++)
            {
                if (TryFindRuntimeAiPlannerAbilitySlotCast(harness, ai.Id.Value, out request))
                {
                    foundAbilityCast = true;
                    break;
                }

                harness.Tick();
            }

            Assert.IsTrue(foundAbilityCast, "Runtime AI Planner did not submit an ability-hotbar command.");
            Assert.AreEqual(1, request.SlotNumber);
            Assert.IsTrue(
                harness.Modules.AbilityHotbar.TryGetAbilityForSlot(
                    request.SlotNumber,
                    out AbilityDefinitionConfig ability));
            Assert.AreEqual(ability.AbilityId, request.AbilityId);
            Assert.AreEqual(0, ai.Input.SkillSlotPressedMask);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballActionId,
                ai.Action.ActiveActionId,
                "The ability-hotbar command must advance the gameplay action runtime.");
            Assert.AreNotEqual(
                CharacterTestSkillSlotRuntime.EmptyRejectReason,
                ai.Action.LastRejectReason);

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTemplateLibrary.SkillCasterProfileId,
                snapshot.ProfileId);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                snapshot.Agents[0].SelectedActionKind);
            Assert.AreEqual(1, snapshot.Agents[0].SelectedAbilitySlot);
        }

    }
}
