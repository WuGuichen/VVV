using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Config;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {

[Test]
        public void GameSlice_DefaultTraceRecorderDoesNotWriteFile()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                Assert.IsFalse(harness.Services.TraceRecorder.WritesFile);
                Assert.AreEqual(string.Empty, harness.Services.TraceRecorder.FilePath);
            }
        }

[Test]
        public void CharacterTestBootstrap_SpawnsTwoHostileTargetsForFireballBurst()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false))
            {
                for (int i = 0; i < 8; i++)
                    slice.Tick(1d / 60d);

                CharacterTestCharacterEntity localPlayer = slice.CharacterEntities.FirstOrDefault(
                    entity => entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer);
                Assert.NotNull(localPlayer);
                CharacterTestCharacterEntity[] hostiles = slice.CharacterEntities
                    .Where(entity => entity.Team.TeamId != localPlayer.Team.TeamId)
                    .OrderBy(entity => entity.Id.Value)
                    .ToArray();

                Assert.AreEqual(2, hostiles.Length);
                Assert.That(hostiles, Has.All.Matches<CharacterTestCharacterEntity>(
                    entity => entity.Control.Owner == CharacterTestCharacterControlOwner.None));
                Assert.That(hostiles, Has.All.Matches<CharacterTestCharacterEntity>(
                    entity => entity.Team.TeamId == 2));
                Assert.AreEqual(-0.25f, hostiles[0].Transform.X, 0.001f);
                Assert.AreEqual(4f, hostiles[0].Transform.Z, 0.001f);
                Assert.AreEqual(1.25f, hostiles[1].Transform.X, 0.001f);
                Assert.AreEqual(4f, hostiles[1].Transform.Z, 0.001f);
                Assert.Less(
                    Math.Abs(hostiles[1].Transform.X - hostiles[0].Transform.X),
                    2f,
                    "Fireball Burst area radius should cover both default hostile targets.");
            }
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityMovesTowardLocalPlayer()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.Less(controlled.Input.Move.x, 0f);
            Assert.AreEqual(0f, controlled.Input.Move.y);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledAllyPrefersOpposingTeamTarget()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: -1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.Greater(controlled.Input.Move.x, 0f);
            Assert.AreEqual(0f, controlled.Input.Move.y);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityChoosesNearestOpposingTeamTarget()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 5f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -2f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.Less(controlled.Input.Move.x, 0f);
            Assert.AreEqual(0f, controlled.Input.Move.y);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityMovesAlongZAxisTowardTarget()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 4f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.AreEqual(0f, controlled.Input.Move.x);
            Assert.Greater(controlled.Input.Move.y, 0f);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityPressesLightAttackWhenInRange()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.AreEqual(Vector2.zero, controlled.Input.Move);
            Assert.IsFalse(controlled.Input.JumpPressed);
            Assert.IsTrue(controlled.Input.AttackPrimaryPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityDoesNotRepeatLightAttackWhileActionRunning()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            controlled.SetAction(new CharacterTestCharacterActionComponent(
                activeActionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Startup,
                localFrame: 2,
                lastRejectReason: default,
                lastAnimationActionKey: string.Empty,
                animationActionKeyThisFrame: string.Empty,
                animationTransitionSeconds: 0f,
                animationHoldSeconds: 0f,
                jumpImpulseY: 0f));
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.AreEqual(Vector2.zero, controlled.Input.Move);
            Assert.IsFalse(controlled.Input.JumpPressed);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_PreservesLocalPlayerInputAndClearsAiWhenNoTarget()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity localPlayer = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            localPlayer.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.right,
                jumpPressed: true,
                attackPrimaryPressed: true));
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.AreEqual(Vector2.right, localPlayer.Input.Move);
            Assert.IsTrue(localPlayer.Input.JumpPressed);
            Assert.IsTrue(localPlayer.Input.AttackPrimaryPressed);

            world.Clear();
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);

            system.Tick(world);

            Assert.AreEqual(Vector2.zero, controlled.Input.Move);
            Assert.IsFalse(controlled.Input.JumpPressed);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_AiControlledEntityClearsInputWhenOnlySameTeamEntitiesExist()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            controlled.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.right,
                jumpPressed: true,
                attackPrimaryPressed: true));
            var system = new CharacterTestRuntimeAiPlannerControlSystem();

            system.Tick(world);

            Assert.AreEqual(Vector2.zero, controlled.Input.Move);
            Assert.IsFalse(controlled.Input.JumpPressed);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_ProfileAttackRangeControlsLightAttack()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            var profile = new CharacterTestAiBehaviorProfile(attackRange: 0.5f);
            var system = new CharacterTestRuntimeAiPlannerControlSystem(profile: profile);

            system.Tick(world);

            Assert.Less(controlled.Input.Move.x, 0f);
            Assert.AreEqual(0f, controlled.Input.Move.y);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_ProfileActionIdsDriveMoveAndLightAttack()
        {
            var farWorld = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            farWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity farControlled = farWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            var profile = new CharacterTestAiBehaviorProfile(
                moveToTargetActionId: 42,
                lightAttackActionId: 43);
            var system = new CharacterTestRuntimeAiPlannerControlSystem(profile: profile);

            system.Tick(farWorld);

            Assert.Less(farControlled.Input.Move.x, 0f);
            Assert.IsFalse(farControlled.Input.AttackPrimaryPressed);

            var nearWorld = new CharacterTestCharacterRuntimeWorld();
            nearWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity nearControlled = nearWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);

            system.Tick(nearWorld);

            Assert.AreEqual(Vector2.zero, nearControlled.Input.Move);
            Assert.IsTrue(nearControlled.Input.AttackPrimaryPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_ProfileCandidateAnglesDriveFirstClearNavigationChoice()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: true);
            var profile = new CharacterTestAiBehaviorProfile(
                navigationCandidateAngles: new[] { -45f, 45f });
            var system = new CharacterTestRuntimeAiPlannerControlSystem(
                navigationService: new CharacterTestCombatPhysicsNavigationService(physicsRuntime, profile),
                profile: profile);

            system.Tick(world);

            Assert.Greater(controlled.Input.Move.x, 0f);
            Assert.Less(controlled.Input.Move.y, 0f);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_ProfileSourceNormalizesInvalidValuesToDefaults()
        {
            var invalid = new CharacterTestAiBehaviorProfile(
                attackRange: -1f,
                plannerMaxDepth: 0,
                moveToTargetActionId: -10,
                lightAttackActionId: -20,
                targetSelectionMode: (CharacterTestAiTargetSelectionMode)999,
                navigationProbeMinLength: 4f,
                navigationProbeMaxLength: 2f,
                navigationCandidateAngles: Array.Empty<float>());

            CharacterTestAiBehaviorProfile normalized =
                CharacterTestAiBehaviorProfileSource.Normalize(invalid);

            Assert.AreEqual(CharacterTestAiBehaviorProfileSource.DefaultAttackRange, normalized.AttackRange);
            Assert.AreEqual(CharacterTestAiBehaviorProfileSource.DefaultPlannerMaxDepth, normalized.PlannerMaxDepth);
            Assert.AreEqual(
                CharacterTestAiBehaviorProfileSource.DefaultMoveToTargetActionId,
                normalized.MoveToTargetActionId);
            Assert.AreEqual(
                CharacterTestAiBehaviorProfileSource.DefaultLightAttackActionId,
                normalized.LightAttackActionId);
            Assert.AreEqual(
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                normalized.TargetSelectionMode);
            Assert.AreEqual(
                CharacterTestAiBehaviorProfileSource.DefaultNavigationProbeMinLength,
                normalized.NavigationProbeMinLength);
            Assert.AreEqual(
                CharacterTestAiBehaviorProfileSource.DefaultNavigationProbeMaxLength,
                normalized.NavigationProbeMaxLength);
            Assert.AreEqual(4, normalized.NavigationCandidateAngles.Count);
            Assert.AreEqual(45f, normalized.NavigationCandidateAngles[0]);
            Assert.AreEqual(-45f, normalized.NavigationCandidateAngles[1]);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_CombatPhysicsNavigationSteersAroundBlockedDirectPath()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: true);
            var system = new CharacterTestRuntimeAiPlannerControlSystem(
                navigationService: new CharacterTestCombatPhysicsNavigationService(physicsRuntime));

            system.Tick(world);

            Assert.Greater(controlled.Input.Move.x, 0f);
            Assert.Greater(controlled.Input.Move.y, 0f);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_CombatPhysicsNavigationDoesNotAttackThroughBlockedPath()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 2.7f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1.5f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: true);
            var system = new CharacterTestRuntimeAiPlannerControlSystem(
                navigationService: new CharacterTestCombatPhysicsNavigationService(physicsRuntime));

            system.Tick(world);

            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_CombatPhysicsNavigationKeepsDirectMoveWhenPathIsClear()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: -4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: true);
            var system = new CharacterTestRuntimeAiPlannerControlSystem(
                navigationService: new CharacterTestCombatPhysicsNavigationService(physicsRuntime));

            system.Tick(world);

            Assert.Less(controlled.Input.Move.x, 0f);
            Assert.Less(Mathf.Abs(controlled.Input.Move.y), 0.1f);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

        [Test]
        public void CombatPhysicsNavigationService_KeepsDirectResultWhenCapsulePathIsClear()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: -4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: true);
            var navigation = new CharacterTestCombatPhysicsNavigationService(physicsRuntime);

            Assert.IsTrue(navigation.TryResolveMove(
                controlled,
                target,
                CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
                out CharacterTestAiNavigationResult result));

            Assert.IsTrue(result.DirectPathClear);
            Assert.AreEqual(-1, result.SelectedCandidateIndex);
            Assert.Less(result.Move.x, 0f);
            Assert.AreEqual(0f, result.Move.y, 0.0001f);
        }

[Test]
        public void RuntimeAiPlannerControlSystem_CombatPhysicsNavigationStopsWhenAllCandidatesAreBlocked()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity controlled = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateBlockedNavigationPhysicsRuntime();
            var system = new CharacterTestRuntimeAiPlannerControlSystem(
                navigationService: new CharacterTestCombatPhysicsNavigationService(physicsRuntime));

            system.Tick(world);

            Assert.AreEqual(Vector2.zero, controlled.Input.Move);
            Assert.IsFalse(controlled.Input.AttackPrimaryPressed);
            Assert.IsFalse(controlled.Input.JumpPressed);
        }

[Test]
        public void RuntimeCommandModule_SpawnCommand_DrainsThroughCommandBoundaryAndQueuesSpawn()
        {
            var characterRuntime = new CharacterTestCharacterRuntimeModule(null, NullRuntimeLogger.Instance);
            var commands = new CharacterTestRuntimeCommandModule(characterRuntime, NullRuntimeLogger.Instance);
            try
            {
                var frame = new RuntimeFrame(7);

                RuntimeCommandValidationResult enqueue = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.SpawnCharacter(
                        frame,
                        CharacterTestRuntimeCommandSources.StoryFlow,
                        CharacterTestCharacterSpawnSpec.TemporaryPlayer,
                        traceId: new RuntimeTraceIdentity(995740)));

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                Assert.AreEqual(1, commands.PendingCommandCount);

                commands.Tick(new RuntimeTickContext(
                    frame.Value,
                    deltaTime: 0.016d,
                    elapsedTime: 0d,
                    RuntimeTickStage.Simulation));

                Assert.AreEqual(0, commands.PendingCommandCount);
                Assert.AreEqual(1, commands.LastDrainedCommands.Count);
                Assert.AreEqual(CharacterTestRuntimeCommandIds.SpawnCharacter, commands.LastDrainedCommands[0].CommandId);
                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.That(commands.RecentEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.CommandAccepted));
                Assert.That(commands.RecentEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.CharacterSpawnQueued));
                Assert.IsEmpty(characterRuntime.Entities, "The command boundary should enqueue spawn work, not mutate the runtime world directly.");
            }
            finally
            {
                commands.Dispose();
                characterRuntime.Dispose();
            }
        }

[Test]
        public void RuntimeCommandModule_TickDrainDoesNotAllocateAfterWarmup()
        {
            var commands = new CharacterTestRuntimeCommandModule(null, NullRuntimeLogger.Instance);
            try
            {
                for (int frame = 0; frame < 8; frame++)
                {
                    EnqueueCameraModeCommands(commands, new RuntimeFrame(frame));
                    commands.Tick(new RuntimeTickContext(
                        frame,
                        deltaTime: 0.016d,
                        elapsedTime: 0d,
                        RuntimeTickStage.Simulation));
                    Assert.AreEqual(4, commands.LastDrainedCommands.Count);
                }

                for (int frame = 8; frame < 72; frame++)
                    EnqueueCameraModeCommands(commands, new RuntimeFrame(frame));

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 8; frame < 72; frame++)
                {
                    commands.Tick(new RuntimeTickContext(
                        frame,
                        deltaTime: 0.016d,
                        elapsedTime: 0d,
                        RuntimeTickStage.Simulation));
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
                Assert.AreEqual(4, commands.LastDrainedCommands.Count);
                Assert.AreEqual(0, commands.PendingCommandCount);
                Assert.IsEmpty(commands.LastCommandErrors);
            }
            finally
            {
                commands.Dispose();
            }
        }

[Test]
        public void RuntimeCommandModule_BlockedContinuousLocalControlIsNoAllocNoOp()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                startupCharacterSpawnSpecs:
                    Array.Empty<CharacterTestCharacterSpawnSpec>());
            CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
            CharacterTestRuntimeCommandModule commands =
                slice.RuntimeCommands;
            long startFrame = slice.CurrentFrame.Value;
            entity.BeginDeath(
                startFrame,
                sourceEntityId: 0,
                traceId: 0);

            for (int offset = 0; offset < 72; offset++)
            {
                RuntimeCommandValidationResult enqueue =
                    commands.Enqueue(
                        CharacterTestRuntimeCommandFactory
                            .SetControlIntent(
                                new RuntimeFrame(startFrame + offset),
                                CharacterTestRuntimeCommandSources
                                    .PlayerInput,
                                entity.Id.Value,
                                Vector2.zero,
                                jumpPressed: false,
                                jumpHeld: false,
                                jumpReleased: false,
                                attackPrimaryPressed: false,
                                traceId:
                                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0014)));
                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
            }

            for (int offset = 0; offset < 8; offset++)
            {
                commands.Tick(new RuntimeTickContext(
                    startFrame + offset,
                    deltaTime: 0.016d,
                    elapsedTime: 0d,
                    RuntimeTickStage.Simulation));
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int offset = 8; offset < 72; offset++)
            {
                commands.Tick(new RuntimeTickContext(
                    startFrame + offset,
                    deltaTime: 0.016d,
                    elapsedTime: 0d,
                    RuntimeTickStage.Simulation));
            }
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.IsEmpty(commands.LastCommandErrors);
            Assert.That(
                commands.RecentEvents,
                Has.None.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type
                        == CharacterTestRuntimeEventType
                            .ControlIntentRejected));

            long explicitFrame = startFrame + 72;
            RuntimeCommandValidationResult explicitEnqueue =
                commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetControlIntent(
                        new RuntimeFrame(explicitFrame),
                        CharacterTestRuntimeCommandSources.DebugUi,
                        entity.Id.Value,
                        Vector2.zero,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: false,
                        traceId: new RuntimeTraceIdentity(903198)));
            Assert.IsTrue(
                explicitEnqueue.Success,
                explicitEnqueue.Error.Message);

            commands.Tick(new RuntimeTickContext(
                explicitFrame,
                deltaTime: 0.016d,
                elapsedTime: 0d,
                RuntimeTickStage.Simulation));

            Assert.AreEqual(1, commands.LastCommandErrors.Count);
            Assert.That(
                commands.LastCommandErrors[0].Message,
                Does.Contain("cannot receive control intent"));
        }

[Test]
        public void RuntimeCommandModule_SurfaceProbeAppliesAuditableMotionCommand()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false,
                enableLocalInputCommands: false);
            CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);

            RuntimeCommandValidationResult enqueue = slice.EnqueueSurfaceProbe(
                CharacterTestSurfaceProbeOperation.ApplyPush,
                entity.Id.Value,
                CharacterTestRuntimeCommandSources.DebugUi,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0015));
            slice.Tick(1d / 60d);

            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
            Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                command => command.CommandId == CharacterTestRuntimeCommandIds.SurfaceProbe));
            Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
            Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                evt => evt.Type == CharacterTestRuntimeEventType.SurfaceProbeApplied
                    && evt.EntityId == entity.Id.Value));
            Assert.Greater(entity.HorizontalVelocity.magnitude, 0.1f);
        }

[Test]
        public void LocalInputCommandModule_UiContextBlocksGameplayRestoreUntilClosed()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false,
                inputProvider: new FixedInputProvider(InputSnapshot.Empty));
            CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
            var input = new TrackingInputProvider(
                CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true),
                InputContext.Disabled);
            IDisposable debugScope = input.PushContext(InputContext.Debug, InputContextPolicy.Overlay);
            IDisposable uiScope = input.PushContext(InputContext.UI, InputContextPolicy.Exclusive);
            try
            {
                slice.SetLocalInputProvider(input);

                Assert.AreEqual(InputContext.UI, input.CurrentContext);
                Assert.IsTrue(input.ContainsContext(InputContext.UI));
                Assert.IsTrue(input.ContainsContext(InputContext.Debug));
                Assert.IsTrue(input.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Debug));
                Assert.AreEqual(2, input.ActiveScopeCount);

                slice.Tick(1d / 60d);

                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.AreEqual(Vector2.zero, entity.Input.Move);
                Assert.IsFalse(entity.Input.AttackPrimaryPressed);

                uiScope.Dispose();
                uiScope = null;
                Assert.IsFalse(input.ContainsContext(InputContext.UI));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsTrue(input.IsContextEnabled(InputContext.Debug));

                slice.Tick(1d / 60d);

                Assert.IsTrue(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsTrue(input.IsContextEnabled(InputContext.Debug));
                Assert.AreEqual(Vector2.right, entity.Input.Move);
                Assert.IsTrue(entity.Input.AttackPrimaryPressed);
            }
            finally
            {
                uiScope?.Dispose();
                debugScope?.Dispose();
            }
        }

[Test]
        public void LocalInputCommandModule_DrainsQueuedPressCommandsForFixedTick()
        {
            var input = new TrackingInputProvider(InputSnapshot.Empty);
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false,
                inputProvider: input);
            CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
            input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));
            input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.AttackSecondary));
            input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.SkillSlot3));

            slice.Tick(1d / 60d);

            Assert.IsTrue(entity.Input.JumpPressed);
            Assert.IsTrue(entity.Input.AttackPrimaryPressed);
            Assert.IsTrue(entity.Input.AttackSecondaryPressed);
            Assert.AreEqual(CharacterTestSkillSlotRuntime.ToMask(2), entity.Input.SkillSlotPressedMask);
            Assert.AreEqual(0, input.Commands.PendingCount);

            slice.Tick(1d / 60d);

            Assert.IsFalse(entity.Input.JumpPressed);
            Assert.IsFalse(entity.Input.AttackPrimaryPressed);
            Assert.IsFalse(entity.Input.AttackSecondaryPressed);
            Assert.AreEqual(0, entity.Input.SkillSlotPressedMask);
        }

[Test]
        public void LocalInputCommandModule_QueuedJumpStartsWithoutAttack()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(8));
                var input = new TrackingInputProvider(InputSnapshot.Empty);
                using var slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    }),
                    baseResourcePreloadGroupId: string.Empty,
                    actionConfigOptions: new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }),
                    createCharacterViews: false,
                    inputProvider: input);
                CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
                entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
                input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.Jump));

                slice.Tick(1d / 60d);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                Assert.Greater(entity.VerticalVelocity, 0f);
                Assert.AreEqual(0, entity.JumpBufferFramesRemaining);
                Assert.AreEqual(0, entity.PendingGameplayIntentCount);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void LocalInputCommandModule_InputServiceSnapshotPressAppliesOnceAcrossCatchUpTicks()
        {
            string directory = CreateTempActionConfigDirectory();
            GameObject inputOwner = new GameObject("CharacterTest Scripted InputService");
            var input = inputOwner.AddComponent<ScriptedInputService>();
            input.enabled = false;
            try
            {
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(24));
                input.SetContext(InputContext.Gameplay);
                using var slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    }),
                    baseResourcePreloadGroupId: string.Empty,
                    actionConfigOptions: new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }),
                    createCharacterViews: false,
                    inputProvider: input);
                CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
                entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
                input.SetSnapshotForTests(
                    CreateInputSnapshot(Vector2.zero, jumpPressed: true, jumpHeld: true),
                    lastCommandFrame: 12);
                input.Commands.Enqueue(new InputCommand(12, 1, InputIntent.Jump));

                slice.Tick(1d / 60d);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                Assert.Greater(entity.VerticalVelocity, 0f);
                Assert.AreEqual(0, entity.JumpBufferFramesRemaining);
                Assert.AreEqual(0, entity.PendingGameplayIntentCount);

                slice.Tick(1d / 60d);

                Assert.IsFalse(entity.Input.JumpPressed);
                Assert.IsTrue(entity.Input.JumpHeld);
                Assert.AreEqual(0, entity.JumpBufferFramesRemaining);
                Assert.AreEqual(0, entity.PendingGameplayIntentCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputOwner);
                DeleteDirectory(directory);
            }
        }

[Test]
        public void LocalInputCommandModule_InputServiceHotbarPressAppliesOnceAcrossCatchUpTicks()
        {
            GameObject inputOwner = new GameObject("CharacterTest Scripted InputService");
            var input = inputOwner.AddComponent<ScriptedInputService>();
            var hotbarInput = new FixedAbilityHotbarInputSource(1);
            input.enabled = false;
            try
            {
                input.SetContext(InputContext.Gameplay);
                using var slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    }),
                    baseResourcePreloadGroupId: string.Empty,
                    createCharacterViews: false,
                    inputProvider: input);
                slice.Services.Modules.LocalInputCommands.SetAbilityHotbarInputSource(hotbarInput);
                SpawnLocalPlayer(slice);
                input.SetSnapshotForTests(InputSnapshot.Empty, lastCommandFrame: 12);

                slice.Tick(1d / 60d);

                Assert.AreEqual(1, hotbarInput.ReadCount);

                slice.Tick(1d / 60d);

                Assert.AreEqual(1, hotbarInput.ReadCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputOwner);
            }
        }

[Test]
        public void LocalInputCommandModule_QueuedJumpSurvivesAttackActionBlock()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(24));
                var input = new TrackingInputProvider(InputSnapshot.Empty);
                using var slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    }),
                    baseResourcePreloadGroupId: string.Empty,
                    actionConfigOptions: new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }),
                    createCharacterViews: false,
                    inputProvider: input);
                CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
                entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
                input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));
                input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.Jump));

                slice.Tick(1d / 60d);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
                Assert.Greater(entity.PendingGameplayIntentCount, 0);

                for (int i = 0;
                    i < CharacterTestGameplayIntentBuffer.DefaultBufferFrames && !string.Equals(
                        (entity.Action.ActiveActionId).ToString(),
                        (CharacterTestCharacterActionConfigSource.JumpActionId).ToString(),
                        StringComparison.Ordinal);
                    i++)
                {
                    slice.Tick(1d / 60d);
                }

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                Assert.Greater(entity.VerticalVelocity, 0f);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void LocalInputCommandModule_BlockedContextDropsQueuedPressCommands()
        {
            var input = new TrackingInputProvider(InputSnapshot.Empty, InputContext.Disabled);
            IDisposable uiScope = input.PushContext(InputContext.UI, InputContextPolicy.Exclusive);
            try
            {
                using var slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    }),
                    baseResourcePreloadGroupId: string.Empty,
                    createCharacterViews: false,
                    inputProvider: input);
                CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));
                input.Commands.Enqueue(new InputCommand(0, 1, InputIntent.Jump));

                slice.Tick(1d / 60d);

                Assert.IsFalse(entity.Input.AttackPrimaryPressed);
                Assert.IsFalse(entity.Input.JumpPressed);
                Assert.AreEqual(0, entity.PendingGameplayIntentCount);
                Assert.AreEqual(0, input.Commands.PendingCount);

                uiScope.Dispose();
                uiScope = null;
                slice.Tick(1d / 60d);

                Assert.IsFalse(entity.Input.AttackPrimaryPressed);
                Assert.IsFalse(entity.Input.JumpPressed);
            }
            finally
            {
                uiScope?.Dispose();
            }
        }

[Test]
        public void RuntimeCommandModule_TargetLockWithoutRuntimeEntity_ReportsRejectedEvent()
        {
            var characterRuntime = new CharacterTestCharacterRuntimeModule(null, NullRuntimeLogger.Instance);
            var commands = new CharacterTestRuntimeCommandModule(characterRuntime, NullRuntimeLogger.Instance);
            try
            {
                var frame = new RuntimeFrame(3);

                RuntimeCommandValidationResult enqueue = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        frame,
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primaryEntityId: 0,
                        traceId: new RuntimeTraceIdentity(901604)));
                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                commands.Tick(new RuntimeTickContext(
                    frame.Value,
                    deltaTime: 0.016d,
                    elapsedTime: 0d,
                    RuntimeTickStage.Simulation));

                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(1, commands.LastCommandErrors.Count);
                Assert.That(commands.LastCommandErrors[0].Message, Does.Contain("primary target"));
                Assert.That(commands.RecentEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.TargetLockRejected
                        && evt.CommandId == CharacterTestRuntimeCommandIds.ToggleTargetLock));
            }
            finally
            {
                commands.Dispose();
                characterRuntime.Dispose();
            }
        }

[Test]
        public void SpawnResources_CreateCatalog_ContainsCharacterPrefabAndAnimationEntries()
        {
            ResourceCatalog catalog = CharacterTestCharacterSpawnResources.CreateCatalog();

            Assert.AreEqual(CharacterTestCharacterSpawnResources.CatalogId, catalog.CatalogId);
            Assert.AreEqual(CharacterTestCharacterSpawnResources.PackageId, catalog.PackageId);
            AssertEntry(
                catalog,
                CharacterTestCharacterSpawnResources.DefaultPlayablePreviewPrefabId,
                ResourceTypeIds.GameObject,
                ResourcesProvider.Id,
                "CharacterSpawn");
            AssertEntry(
                catalog,
                CharacterTestCharacterSpawnResources.DefaultMainHandWeaponResourceId,
                ResourceTypeIds.GameObject,
                "editorAssetDatabase",
                "CharacterEquipment");
            AssertEntry(
                catalog,
                CharacterTestCharacterSpawnResources.DefaultOffHandWeaponResourceId,
                ResourceTypeIds.GameObject,
                "editorAssetDatabase",
                "CharacterEquipment");
            Assert.That(catalog.Entries, Has.Some.Matches<ResourceCatalogEntry>(
                entry => entry.TypeId == ResourceTypeIds.AnimationClip
                    && entry.PackageId == CharacterTestCharacterSpawnResources.SampleAnimationPackageId
                    && entry.Labels.Contains("CharacterAnimation")));
        }

[Test]
        public void SpawnResources_CreateCatalog_CanSkipTemporaryAnimationEntries()
        {
            ResourceCatalog catalog = CharacterTestCharacterSpawnResources.CreateCatalog(includeAnimationEntries: false);

            Assert.That(catalog.Entries, Has.Some.Matches<ResourceCatalogEntry>(
                entry => entry.Id == CharacterTestCharacterSpawnResources.DefaultPlayablePreviewPrefabId));
            Assert.That(catalog.Entries, Has.None.Matches<ResourceCatalogEntry>(
                entry => entry.TypeId == ResourceTypeIds.AnimationClip));
        }

[Test]
        public void SpawnResources_CreateCatalog_CanSkipPublicWeaponFallbackEntries()
        {
            ResourceCatalog catalog = CharacterTestCharacterSpawnResources.CreateCatalog(
                includePublicMainHandWeaponFallbackEntry: false,
                includePublicOffHandWeaponFallbackEntry: false);

            Assert.That(catalog.Entries, Has.None.Matches<ResourceCatalogEntry>(
                entry => entry.Id == CharacterTestCharacterSpawnResources.DefaultMainHandWeaponResourceId));
            Assert.That(catalog.Entries, Has.None.Matches<ResourceCatalogEntry>(
                entry => entry.Id == CharacterTestCharacterSpawnResources.DefaultOffHandWeaponResourceId));
            Assert.That(catalog.Entries, Has.Some.Matches<ResourceCatalogEntry>(
                entry => entry.Id == CharacterTestCharacterSpawnResources.DefaultPlayablePreviewPrefabId));
        }

[Test]
        public void SpawnFlow_PreloadedSampleAnimations_StillRegistersDragon14Animations()
        {
            ResourceCatalog completeCatalog = CharacterTestCharacterSpawnResources.CreateCatalog();
            ResourceCatalogEntry sampleEntry = completeCatalog.Entries.FirstOrDefault(
                entry => entry.TypeId == ResourceTypeIds.AnimationClip
                    && entry.PackageId == CharacterTestCharacterSpawnResources.SampleAnimationPackageId);
            ResourceCatalogEntry dragonIdleEntry = completeCatalog.Entries.FirstOrDefault(
                entry => entry.Id == "char.000003.animation.idle1"
                    && entry.TypeId == ResourceTypeIds.AnimationClip
                    && entry.PackageId == "000003");
            if (sampleEntry == null || dragonIdleEntry == null)
                Assert.Ignore("Generated sample or Dragon14 animation package is not present.");

            var resourceManager = new ResourceManager();
            resourceManager.RegisterProvider(new CharacterTestEditorAnimationAssetProvider());
            resourceManager.RegisterProvider(new MxFramework.Editor.EditorAssetDatabaseResourceProvider());
            resourceManager.AddCatalog(new ResourceCatalog(
                "character-test.preloaded-sample-animation",
                CharacterTestCharacterSpawnResources.SampleAnimationPackageId,
                new[] { sampleEntry }));
            using var resources = new GlobalResourceRuntimeServices(
                GlobalResourceRuntimeMode.Editor,
                resourceManager,
                new ResourcePreloadService(resourceManager));

            var flow = new CharacterTestCharacterSpawnFlow(
                resources,
                NullRuntimeLogger.Instance,
                runtimeCommands: null,
                runtimeModule: null,
                currentFrameProvider: null);
            try
            {
                var dragonIdleKey = new ResourceKey(
                    "char.000003.animation.idle1",
                    ResourceTypeIds.AnimationClip,
                    packageId: "000003");
                Assert.IsTrue(resourceManager.Contains(dragonIdleKey), "Dragon14 idle was not registered by the spawn flow.");
                Assert.IsTrue(
                    resourceManager.TryGetEntry(dragonIdleKey, out ResourceCatalogEntry registeredEntry),
                    "Dragon14 idle catalog entry was not queryable after registration.");
                Assert.IsTrue(CharacterRuntimeAnimationResources.TryGetCatalogEntryUnityAssetPath(
                    registeredEntry,
                    out string assetPath), "Dragon14 idle catalog entry has no Unity asset path.");
                StringAssert.EndsWith(".anim", assetPath.ToLowerInvariant());
            }
            finally
            {
                flow.Release();
            }
        }

[Test]
        public void GameSlice_ConstructsStartedHostWithCharacterTestModules()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);

            RuntimeHostDiagnostics diagnostics = slice.CaptureDiagnostics();

            Assert.AreEqual(RuntimeLifecycleState.Started, diagnostics.State);
            Assert.That(diagnostics.Modules, Has.Some.Matches<RuntimeModuleDiagnostics>(
                module => module.ModuleId == StoryRuntimeModule.DefaultModuleId));
            Assert.That(diagnostics.Modules, Has.Some.Matches<RuntimeModuleDiagnostics>(
                module => module.ModuleId == "CharacterTest.RuntimeCommands"));
            Assert.That(diagnostics.Modules, Has.Some.Matches<RuntimeModuleDiagnostics>(
                module => module.ModuleId == "CharacterTest.CharacterRuntime"));
            Assert.GreaterOrEqual(diagnostics.TickCount, 1);
            Assert.IsTrue(slice.StorySnapshot.ActiveBeatInstances.Count > 0);
            Assert.IsFalse(slice.Resources.HasBootstrapError, slice.Resources.BootstrapErrorMessage);
            Assert.AreEqual(GlobalResourceRuntimeMode.Editor, slice.Resources.Mode);
            Assert.IsTrue(slice.Resources.ResourceManager.Contains(CharacterTestFairyGuiCombatHudIds.PackageBytesKey));
            Assert.IsTrue(slice.Resources.ResourceManager.Contains(CharacterTestFairyGuiCombatHudIds.Atlas0Key));
            Assert.IsTrue(slice.Resources.ResourceManager.Contains(new ResourceKey("scene.character_test", ResourceTypeIds.Scene)));
            Assert.IsTrue(slice.HasSceneFlow);
            Assert.NotNull(slice.SceneFlowController);

            RuntimeHostDiagnostics sceneFlowDiagnostics = slice.CaptureDiagnostics();
            Assert.That(sceneFlowDiagnostics.Modules, Has.Some.Matches<RuntimeModuleDiagnostics>(
                module => module.ModuleId == "CharacterTest.SceneFlow"));

            SceneFlowResult rejected = slice.RequestSceneLoad(string.Empty, SceneFlowLoadMode.Additive);
            Assert.AreEqual(SceneFlowResultStatus.Failed, rejected.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, rejected.OperationType);
            Assert.AreEqual(SceneFlowErrorCode.InvalidRequest, rejected.Error.Code);
        }

[Test]
        public void GameSlice_OfflineResources_RegistersResourceSceneFlowModule()
        {
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = GlobalResourceRuntimeMode.Offline,
                AllowMissingPreloadGroupInEditor = false
            };
            GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(options);

            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            ResourceManager manager = slice.Resources.ResourceManager;
            var sceneKey = new ResourceKey("scene.character_test", ResourceTypeIds.Scene);

            Assert.IsFalse(slice.Resources.HasBootstrapError, slice.Resources.BootstrapErrorMessage);
            Assert.IsTrue(manager.Contains(sceneKey));
            Assert.IsTrue(slice.Resources.PreloadGroups.TryGetPlan("SpawnCritical", out ResourcePreloadPlan spawnCritical));
            CollectionAssert.Contains(spawnCritical.Labels, "bundle.spawn.critical");
            Assert.IsTrue(slice.HasSceneFlow);
            Assert.NotNull(slice.SceneFlowController);

            RuntimeHostDiagnostics diagnostics = slice.CaptureDiagnostics();
            Assert.That(diagnostics.Modules, Has.Some.Matches<RuntimeModuleDiagnostics>(
                module => module.ModuleId == "CharacterTest.SceneFlow"));

            SceneFlowSnapshot snapshot = slice.CaptureSceneFlowSnapshot();
            Assert.NotNull(snapshot);
            Assert.IsFalse(snapshot.IsBusy);
            Assert.AreEqual(SceneFlowResultStatus.None, snapshot.LastResult.Status);

            SceneFlowResult rejected = slice.RequestSceneLoad(string.Empty, SceneFlowLoadMode.Additive);
            Assert.AreEqual(SceneFlowResultStatus.Failed, rejected.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, rejected.OperationType);
            Assert.AreEqual(SceneFlowErrorCode.InvalidRequest, rejected.Error.Code);
        }

[Test]
        public void GameManager_KeepsEditorModeWhenResourceManagedSceneFlowIsPreferred()
        {
            CharacterTestSceneProfile profile = CharacterTestSceneProfile.LoadDefault();
            Assert.AreEqual(GlobalResourceRuntimeMode.Editor, profile.ResourceMode);
            UnityEngine.Object.DestroyImmediate(profile);
        }

[Test]
        public void GameManager_DefaultNetworkSessionOptions_UseRuntimeUniqueClientPeerId()
        {
            var host = new GameObject("CharacterTest GameManager Runtime Peer Id Test");
            host.SetActive(false);
            try
            {
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host);

                CharacterTestNetworkSessionOptions hostOptions =
                    context.NetworkCoordinator.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Host);
                CharacterTestNetworkSessionOptions joinOptions =
                    context.NetworkCoordinator.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Join);

                Assert.AreEqual(CharacterTestNetworkSessionKind.Host, hostOptions.Kind);
                Assert.AreEqual(CharacterTestNetworkSessionKind.Join, joinOptions.Kind);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultBindAddress, hostOptions.BindAddress);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultRemoteAddress, joinOptions.RemoteAddress);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultPort, hostOptions.Port);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultPort, joinOptions.Port);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultServerPeerId, hostOptions.ServerPeerId);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultServerPeerId, joinOptions.ServerPeerId);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultRoomId, hostOptions.RoomId);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultRoomName, hostOptions.RoomName);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultDisplayName, joinOptions.DisplayName);
                StringAssert.StartsWith("client-", hostOptions.ClientPeerId);
                Assert.AreEqual(hostOptions.ClientPeerId, joinOptions.ClientPeerId);
                Assert.AreNotEqual(CharacterTestNetworkSessionOptions.DefaultClientPeerId, hostOptions.ClientPeerId);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultInputDelayFrames, hostOptions.InputDelayFrames);
                Assert.AreEqual(CharacterTestNetworkSessionOptions.DefaultSnapshotIntervalFrames, hostOptions.SnapshotIntervalFrames);
                Assert.AreEqual(NetworkStateSyncPolicy.BootstrapOnly, hostOptions.StateSyncPolicy);
                Assert.AreEqual(NetworkStateSyncPolicy.BootstrapOnly, joinOptions.StateSyncPolicy);
                Assert.AreEqual(NetworkSessionConfig.DefaultHeartbeatIntervalFrames, hostOptions.HeartbeatIntervalFrames);
                Assert.AreEqual(NetworkSessionConfig.DefaultHeartbeatIntervalFrames, joinOptions.HeartbeatIntervalFrames);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void GameManager_RoomHostEnterGame_StartsRoomAndWaitsForSynchronization()
        {
            var host = new GameObject("CharacterTest GameManager Room Enter Test");
            host.SetActive(false);
            GameSlice slice = null;
            try
            {
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host);
                GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(
                    new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    });
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                var runner = new RecordingLobbyNetworkRunner(slice)
                {
                    CanStartRoomGame = true,
                    Diagnostics = CreateNetworkPanelDiagnostics(
                        roomStatus: NetworkRoomStatus.Open,
                        roomReadyCount: 2,
                        roomConnectedCount: 2)
                };
                CharacterTestNetworkSessionOptions options =
                    CharacterTestNetworkSessionOptions.HostDefaults(7777, "0.0.0.0", "server", "host-player");
                options.LaunchMode = CharacterTestNetworkLaunchMode.Room;

                context.Slice = slice;
                context.NetworkRunner = runner;
                context.ActiveNetworkSessionKind = CharacterTestNetworkSessionKind.Host;
                context.ActiveNetworkSessionOptions = options;

                CharacterTestNetworkSessionResult result = context.NetworkCoordinator.EnterNetworkGameplay();

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(1, runner.StartRoomCount);
                Assert.IsTrue(context.NetworkGameplayEnterPending);
                CharacterTestNetworkSessionStatus status = context.NetworkCoordinator.CaptureNetworkSessionStatus();
                Assert.AreEqual(NetworkRoomStatus.InGame, status.RoomStatus);
                Assert.IsTrue(status.NetworkGameplayEnterPending);
                Assert.IsFalse(status.CanRequestEnterGameplay);
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                slice?.Dispose();
            }
        }

[Test]
        public void GameManager_AutoSyncProbeTrigger_StartsAfterNetworkGameplayEnter()
        {
            var host = new GameObject("CharacterTest GameManager Auto Probe Test");
            host.SetActive(false);
            GameSlice slice = null;
            try
            {
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host);
                GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(
                    new GlobalResourceRuntimeOptions
                    {
                        AllowMissingPreloadGroupInEditor = true
                    });
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                int submittedCommandCount = 0;
                slice.SetRuntimeCommandSubmitter(command =>
                {
                    submittedCommandCount++;
                    Assert.AreEqual(CharacterTestNetworkSyncProbe.TraceId, command.TraceId);
                    return RuntimeCommandValidationResult.Accepted(command);
                });
                var runner = new RecordingLobbyNetworkRunner(slice)
                {
                    Diagnostics = CreateNetworkPanelDiagnostics(
                        roomStatus: NetworkRoomStatus.Closed,
                        roomReadyCount: 0,
                        roomConnectedCount: 1,
                        clientStatus: NetworkSessionStatus.Connected,
                        ownedEntityId: 7,
                        fullSnapshotReceivedCount: 1)
                };
                CharacterTestNetworkSessionOptions options =
                    CharacterTestNetworkSessionOptions.JoinDefaults("127.0.0.1", 7777, "server", "client-auto");
                options.LaunchMode = CharacterTestNetworkLaunchMode.DirectLan;

                context.Slice = slice;
                context.NetworkRunner = runner;
                context.ActiveNetworkSessionKind = CharacterTestNetworkSessionKind.Join;
                context.ActiveNetworkSessionOptions = options;

                context.NetworkCoordinator.SetNetworkSyncProbeAutoRun(true);
                CharacterTestNetworkSessionResult result = context.NetworkCoordinator.EnterNetworkGameplay();

                Assert.IsTrue(result.Success, result.Message);
                CharacterTestNetworkSyncProbeStatus armedStatus =
                    context.NetworkCoordinator.CaptureNetworkSyncProbeStatus();
                Assert.IsTrue(armedStatus.AutoRunEnabled);
                Assert.IsTrue(armedStatus.AutoRunPending);
                Assert.IsFalse(armedStatus.IsRunning);

                context.NetworkCoordinator.TickNetworkSyncProbe(1d / 60d);

                CharacterTestNetworkSyncProbeStatus runningStatus =
                    context.NetworkCoordinator.CaptureNetworkSyncProbeStatus();
                Assert.IsTrue(runningStatus.AutoRunEnabled);
                Assert.IsFalse(runningStatus.AutoRunPending);
                Assert.IsTrue(runningStatus.IsRunning);
                Assert.AreEqual(0, submittedCommandCount);
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                slice?.Dispose();
            }
        }

[Test]
        public void GameManager_SettingsPanelEntry_RequiresSettingsRuntime()
        {
            var host = new GameObject("CharacterTest GameManager Settings Entry Test");
            host.SetActive(false);
            try
            {
                CharacterTestSceneProfile profile = CharacterTestSceneProfile.LoadDefault();
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host, profile);
                context.SettingsRuntime = null;

                Assert.IsFalse(context.SessionBuilder.ShouldEnableFairyGuiSettingsPanel());

                var settingsRuntime = new CharacterTestSettingsRuntime(
                    "settings.json",
                    new MxSettingsService(null),
                    new MxInputRebindingSettingsController(null, null),
                    MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()));
                context.SettingsRuntime = settingsRuntime;

                Assert.IsTrue(context.SessionBuilder.ShouldEnableFairyGuiSettingsPanel());
                UnityEngine.Object.DestroyImmediate(profile);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void GameManager_MainMenuPauseMode_UsesDefaultPausePolicy()
        {
            var host = new GameObject("CharacterTest GameManager Main Menu Pause Mode Test");
            host.SetActive(false);
            try
            {
                CharacterTestSceneProfile profile = CharacterTestSceneProfile.LoadDefault();
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host, profile);
                context.MainMenuVisible = true;
                context.ManualPaused = false;

                context.MainMenuCoordinator.RefreshEffectivePauseState();

                Assert.IsTrue(context.IsPaused);
                CharacterTestMainMenuStatus liveMenuStatus = context.MainMenuCoordinator.CaptureMainMenuStatus();
                Assert.IsTrue(liveMenuStatus.IsMenuVisible);
                Assert.IsTrue(liveMenuStatus.MenuPausesRuntime);
                Assert.IsTrue(liveMenuStatus.IsPaused);

                context.MainMenuVisible = false;
                context.ManualPaused = true;
                context.MainMenuCoordinator.RefreshEffectivePauseState();

                Assert.IsTrue(context.IsPaused);
                UnityEngine.Object.DestroyImmediate(profile);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void GameManager_EditorMainMenuInput_UsesRuntimePauseAndCancelBindingOverride()
        {
            var host = new GameObject("CharacterTest GameManager Editor Menu Input Test");
            host.SetActive(false);
            try
            {
                CharacterTestRuntimeContext context = CreateCharacterTestRuntimeContext(host);

                IInputProvider provider = context.SessionBuilder.ResolvePlayerUiInputProvider();
                var inputService = provider as InputService;
                Assert.IsNotNull(inputService);

                InputAction pause = inputService.Actions.FindAction("Gameplay/Pause", false);
                InputAction cancel = inputService.Actions.FindAction("UI/Cancel", false);
                Assert.IsNotNull(pause);
                Assert.IsNotNull(cancel);
#if UNITY_EDITOR
                Assert.AreEqual(DefaultInputBindingCatalog.KeyboardPPath, pause.bindings[0].path);
                Assert.AreEqual(DefaultInputBindingCatalog.KeyboardPPath, cancel.bindings[0].path);
                Assert.IsTrue(string.IsNullOrEmpty(pause.bindings[0].overridePath));
                Assert.IsTrue(string.IsNullOrEmpty(cancel.bindings[0].overridePath));
#else
                Assert.AreEqual(DefaultInputBindingCatalog.KeyboardEscapePath, pause.bindings[0].path);
                Assert.AreEqual(DefaultInputBindingCatalog.KeyboardEscapePath, cancel.bindings[0].path);
#endif
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void GameSlice_OfflineResources_MergesExternalRuntimeCatalogAsProviderReference()
        {
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = GlobalResourceRuntimeMode.Offline,
                AllowMissingPreloadGroupInEditor = false
            };
            GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(options);

            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            ResourceManager manager = slice.Resources.ResourceManager;
            var internalKey = new ResourceKey(
                "ui.start_screen.button.normal",
                ResourceTypeIds.Texture2D,
                packageId: "mxframework.samples");
            var externalKey = new ResourceKey(
                "art.character.skeleton.animation.standing_idle",
                ResourceTypeIds.AnimationClip,
                packageId: "mxframework.samples");

            Assert.IsFalse(slice.Resources.HasBootstrapError, slice.Resources.BootstrapErrorMessage);
            Assert.IsTrue(manager.Contains(internalKey));
            Assert.IsTrue(manager.Contains(externalKey));
            Assert.GreaterOrEqual(manager.CreateDebugSnapshot().CatalogCount, 2);
            Assert.GreaterOrEqual(manager.CreateDebugSnapshot().ProviderCount, 3);

            ResourceLoadResult<ResourceHandle<AnimationClip>> externalLoad = manager.Load<AnimationClip>(externalKey);
            Assert.IsTrue(externalLoad.Success, externalLoad.Error.Message);
            Assert.NotNull(externalLoad.Value.Value);
            Assert.AreEqual("standing_idle", externalLoad.Value.Value.name);

            manager.Release(externalLoad.Value);
        }
}
}
