using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Game.CharacterTest
{
    // The two UnityTests enter real Play Mode; synchronous authority ticks remain deterministic inside it.
    public sealed class CharacterTestSlopePlayableBehaviorTests
    {
        [UnityTest]
        public IEnumerator FormalScene_PlayMode_WalkableMatrixUsesFixedTickAuthority()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Scene playModeDriverScene = OpenFormalSceneWithNeutralActiveDriver();
            yield return new EnterPlayMode();

            GameManager manager = null;
            for (int i = 0; i < 120; i++)
            {
                manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (manager != null)
                    break;
                yield return null;
            }

            Exception failure = null;
            try
            {
                Assert.IsNotNull(manager, "Formal CharacterTestScene did not expose GameManager in Play Mode.");
                CharacterTestSlopePlayableFixture.AssertFormalManagerRuntime(manager);
                using (CharacterTestSlopePlayableFixture fixture =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertWalkableTravel(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope15BodyId,
                        CharacterTestSlopePlayableFixture.UphillStartLongitudinalRaw,
                        CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw,
                        expectedYDirection: 1,
                        requireGroundSnap: false,
                        "15.Local.Up");
                    AssertWalkableTravel(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope15BodyId,
                        CharacterTestSlopePlayableFixture.DownhillStartLongitudinalRaw,
                        (short)-CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw,
                        expectedYDirection: -1,
                        requireGroundSnap: true,
                        "15.Local.Down");
                    AssertWalkableTraverse(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope15BodyId,
                        "15.Local.Traverse");

                    AssertWalkableTravel(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        CharacterTestSlopePlayableFixture.UphillStartLongitudinalRaw,
                        CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw,
                        expectedYDirection: 1,
                        requireGroundSnap: false,
                        "30.Local.Up");
                    AssertWalkableTravel(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        CharacterTestSlopePlayableFixture.DownhillStartLongitudinalRaw,
                        (short)-CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw,
                        expectedYDirection: -1,
                        requireGroundSnap: true,
                        "30.Local.Down");
                    AssertWalkableTraverse(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        "30.Local.Traverse");

                    AssertIdleSixHundred(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope15BodyId,
                        "15.Idle");
                    AssertIdleSixHundred(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        "30.Idle");
                    AssertJumpAndLanding(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope15BodyId,
                        "15.JumpLand");
                    AssertJumpAndLanding(
                        fixture,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        "30.JumpLand");
                    AssertEdgeLeave(fixture);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            yield return new ExitPlayMode();
            CloseNeutralActiveDriver(playModeDriverScene);
            Application.runInBackground = previousRunInBackground;
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [UnityTest]
        public IEnumerator FormalScene_PlayMode_SteepAndTypedSourcesUseFixedTickAuthority()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Scene playModeDriverScene = OpenFormalSceneWithNeutralActiveDriver();
            yield return new EnterPlayMode();

            GameManager manager = null;
            for (int i = 0; i < 120; i++)
            {
                manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (manager != null)
                    break;
                yield return null;
            }

            Exception failure = null;
            try
            {
                Assert.IsNotNull(manager, "Formal CharacterTestScene did not expose GameManager in Play Mode.");
                CharacterTestSlopePlayableFixture.AssertFormalManagerRuntime(manager);

                using (CharacterTestSlopePlayableFixture fixture =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertSteepControl(
                        fixture,
                        CharacterTestRuntimeCommandSources.PlayerInput,
                        CharacterTestSlopePlayableFixture.SteepUphillInputRaw,
                        0,
                        "50.Local.Up");
                    AssertSteepControl(
                        fixture,
                        CharacterTestRuntimeCommandSources.PlayerInput,
                        0,
                        CharacterTestSlopePlayableFixture.SteepTraverseInputRaw,
                        "50.Local.Traverse");
                }

                using (CharacterTestSlopePlayableFixture aiFixture =
                       CharacterTestSlopePlayableFixture.Create(
                           manager,
                           CharacterTestCharacterControlOwner.AiControlled))
                {
                    AssertSteepControl(
                        aiFixture,
                        CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                        CharacterTestSlopePlayableFixture.SteepUphillInputRaw,
                        0,
                        "50.Ai.WorldMove");
                }

                using (CharacterTestSlopePlayableFixture dash30 =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertDashStrike(
                        dash30,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        expectWalkableAscent: true,
                        "30.Dash327");
                }
                using (CharacterTestSlopePlayableFixture dash50 =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertDashStrike(
                        dash50,
                        CharacterTestSlopePlayableFixture.Slope50BodyId,
                        expectWalkableAscent: false,
                        "50.Dash327");
                }

                using (CharacterTestSlopePlayableFixture impulse30 =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertImpulse(
                        impulse30,
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        expectWalkableAscent: true,
                        "30.ImpulseRaw");
                }
                using (CharacterTestSlopePlayableFixture impulse50 =
                       CharacterTestSlopePlayableFixture.Create(manager))
                {
                    AssertImpulse(
                        impulse50,
                        CharacterTestSlopePlayableFixture.Slope50BodyId,
                        expectWalkableAscent: false,
                        "50.ImpulseRaw");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            yield return new ExitPlayMode();
            CloseNeutralActiveDriver(playModeDriverScene);
            Application.runInBackground = previousRunInBackground;
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [Test]
        public void FormalScene_ReloadAndCandidateCacheLifecycleProduceIdenticalAuthorityRows()
        {
            Scene firstScene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(firstScene.IsValid() && firstScene.isLoaded);
            Assert.IsFalse(firstScene.isDirty);
            GameManager firstManager = CharacterTestSlopePlayableFixture.FindGameManager(firstScene);

            CharacterTestSlopeAuthorityRow coldFirst;
            CharacterTestSlopeAuthorityRow[] cold;
            CharacterTestSlopeAuthorityRow[] second;
            CharacterTestSlopeAuthorityRow warmFirst;
            CharacterTestSlopeAuthorityRow[] warm;
            CharacterTestSlopeAuthorityRow resetPhysicsFirst;
            CharacterTestSlopeAuthorityRow[] resetPhysics;
            using (CharacterTestSlopePlayableFixture first =
                   CharacterTestSlopePlayableFixture.Create(firstManager))
            {
                first.ReplaceMotionPhysicsRuntime(
                    CharacterTestSlopePlayableFixture.BuildFormalRuntime(firstManager));
                first.PlaceForDeterminismFirstTick();
                coldFirst = first.TickDeterminismFirstTick();
                cold = first.RunDeterminismSequence();
            }
            using (CharacterTestSlopePlayableFixture secondFixture =
                   CharacterTestSlopePlayableFixture.Create(firstManager))
            {
                second = secondFixture.RunDeterminismSequence();
                secondFixture.PlaceForDeterminismFirstTick();
                warmFirst = secondFixture.TickDeterminismFirstTick();
                secondFixture.ResetForDeterminismSequence();
                warm = secondFixture.RunDeterminismSequenceAlreadyPlaced();
                CharacterTestCharacterPhysicsRuntime replacement =
                    CharacterTestSlopePlayableFixture.BuildFormalRuntime(firstManager);
                secondFixture.ReplaceMotionPhysicsRuntime(replacement);
                secondFixture.PlaceForDeterminismFirstTick();
                resetPhysicsFirst = secondFixture.TickDeterminismFirstTick();
                secondFixture.ResetForDeterminismSequence();
                resetPhysics = secondFixture.RunDeterminismSequenceAlreadyPlaced();
            }

            AssertRowEquivalent(coldFirst, warmFirst, "warm first tick");
            AssertRowEquivalent(coldFirst, resetPhysicsFirst, "SetPhysicsRuntime cold first tick");
            AssertRowsEquivalent(cold, second, "fresh second");
            AssertRowsEquivalent(cold, warm, "warm candidate cache");
            AssertRowsEquivalent(cold, resetPhysics, "SetPhysicsRuntime cache reset");
            Assert.IsFalse(firstScene.isDirty);

            Scene reloaded = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(reloaded.IsValid() && reloaded.isLoaded);
            Assert.IsFalse(reloaded.isDirty);
            GameManager reloadedManager = CharacterTestSlopePlayableFixture.FindGameManager(reloaded);
            CharacterTestSlopeAuthorityRow reloadFirst;
            CharacterTestSlopeAuthorityRow[] afterReload;
            using (CharacterTestSlopePlayableFixture reloadFixture =
                   CharacterTestSlopePlayableFixture.Create(reloadedManager))
            {
                reloadFixture.ReplaceMotionPhysicsRuntime(
                    CharacterTestSlopePlayableFixture.BuildFormalRuntime(reloadedManager));
                reloadFixture.PlaceForDeterminismFirstTick();
                reloadFirst = reloadFixture.TickDeterminismFirstTick();
                afterReload = reloadFixture.RunDeterminismSequence();
            }

            AssertRowEquivalent(coldFirst, reloadFirst, "formal Scene reload first tick");
            AssertRowsEquivalent(cold, afterReload, "formal Scene reload");
            Assert.IsFalse(reloaded.isDirty);
        }

        [Test]
        public void FormalCombatGirlCapsule_ThirtyDegreeTrueEdgeDoesNotIterationLimitAndBecomesAirborne()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            Assert.IsFalse(scene.isDirty);
            GameManager manager = CharacterTestSlopePlayableFixture.FindGameManager(scene);

            using (CharacterTestSlopePlayableFixture fixture =
                   CharacterTestSlopePlayableFixture.Create(manager))
            {
                fixture.Entity.SetPhysics(new CharacterTestCharacterPhysicsComponent(
                    capsuleRadiusMeters: 0.58899695f,
                    capsuleHeightMeters: 2.0562358f,
                    capsuleCenterY: 1.0281179f,
                    massKg: 80f));
                fixture.PlaceOnSlope(
                    CharacterTestSlopePlayableFixture.Slope30BodyId,
                    longitudinalRaw: -2000000,
                    lateralRaw: 0,
                    grounded: true,
                    CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
                fixture.SettleWalkable();

                bool crossedGuard = false;
                bool sawAirborneAfterGuard = false;
                for (int i = 0; i < CharacterTestSlopePlayableFixture.EdgeTicks; i++)
                {
                    CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                        -200,
                        0,
                        CharacterTestRuntimeCommandSources.PlayerInput);
                    AssertNoIterationLimit(
                        row,
                        "CombatGirl.30.Edge tick " + i
                        + " position=" + row.XMillimeters + "," + row.YMillimeters + "," + row.ZMillimeters
                        + " grounded=" + row.Grounded
                        + " flags=" + row.CollisionFlags
                        + " diagnostic=" + row.Diagnostic.CollisionFlags
                        + " target=" + row.Diagnostic.TargetBodyId.Value + "." + row.Diagnostic.TargetColliderId.Value
                        + "/" + row.Diagnostic.TargetShapeKind
                        + " normal=" + row.Diagnostic.Normal
                        + " requested=" + row.Diagnostic.RequestedDelta
                        + " applied=" + row.Diagnostic.AppliedDelta
                        + " resolved=" + row.Diagnostic.ResolvedVelocity
                        + " fractionRaw=" + row.Diagnostic.CollisionFraction.RawValue
                        + " distanceRaw=" + row.Diagnostic.CollisionDistance.RawValue);
                    int gap = fixture.ComputeLongitudinalEdgeGapMillimeters(
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        row);
                    if (gap <= CharacterTestSlopePlayableFixture.EdgeGuardMillimeters)
                        continue;

                    crossedGuard = true;
                    bool slopeSnap = row.IsSlopeContact(
                            CharacterTestSlopePlayableFixture.Slope30BodyId)
                        && HasFlag(
                            row.Diagnostic.CollisionFlags,
                            CombatMotionCollisionFlags.GroundSnap);
                    Assert.IsFalse(slopeSnap, "CombatGirl.30.Edge slope snap after gap tick " + i);
                    sawAirborneAfterGuard |= !row.Grounded;
                }

                Assert.IsTrue(crossedGuard, "CombatGirl capsule should exceed the 0.10m support guard.");
                Assert.IsTrue(
                    sawAirborneAfterGuard,
                    "CombatGirl capsule should become airborne after the true 30-degree edge.");
            }

            Assert.IsFalse(scene.isDirty);
        }

        [TestCase(-1000, 0, true)]
        [TestCase(1000, 0, true)]
        [TestCase(-1000, 2051, false)]
        [TestCase(1000, 2051, false)]
        public void FormalCombatGirlCapsule_ThirtyFiftyNarrowGapCanEscapeWithoutFreezing(
            short longitudinalInputRaw,
            int startYMillimeters,
            bool startsGrounded)
        {
            Scene scene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            Assert.IsFalse(scene.isDirty);
            GameManager manager = CharacterTestSlopePlayableFixture.FindGameManager(scene);

            using (CharacterTestSlopePlayableFixture fixture =
                   CharacterTestSlopePlayableFixture.Create(manager))
            {
                fixture.Entity.SetPhysics(new CharacterTestCharacterPhysicsComponent(
                    capsuleRadiusMeters: 0.58899695f,
                    capsuleHeightMeters: 2.0562358f,
                    capsuleCenterY: 1.0281179f,
                    massKg: 80f));
                fixture.Entity.ClearMotionRequests();
                fixture.Entity.SetInput(CharacterTestCharacterInputComponent.None);
                fixture.Entity.SetAction(CharacterTestCharacterActionComponent.None);
                fixture.Entity.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        xMillimeters: startYMillimeters == 2051 ? -23482 : -23000,
                        yMillimeters: startYMillimeters,
                        zMillimeters: startYMillimeters == 2051 ? 16661 : 16750,
                        yawMilliDegrees: longitudinalInputRaw > 0 ? 90000 : 270000));
                float direction = longitudinalInputRaw > 0 ? 1f : -1f;
                fixture.Entity.SetMotionState(
                    verticalVelocity: 0f,
                    lastMoveInput: new Vector2(direction, 0f),
                    isGrounded: startsGrounded,
                    horizontalVelocity: new Vector2(direction * 6f, 0f),
                    desiredVelocity: new Vector2(direction * 6f, 0f),
                    movementMode: CharacterMovementMode.Run,
                    physicsEnabled: true,
                    collisionFlags: startsGrounded
                        ? CombatMotionCollisionFlags.Grounded
                        : CombatMotionCollisionFlags.None,
                    surfaceMaterial: CombatSurfaceMaterial.Default);

                int startX = fixture.Entity.Transform.XMillimeters;
                int startZ = fixture.Entity.Transform.ZMillimeters;
                CharacterTestSlopeAuthorityRow last = default;
                for (int i = 0; i < 90; i++)
                {
                    last = fixture.TickControl(
                        longitudinalInputRaw,
                        0,
                        CharacterTestRuntimeCommandSources.PlayerInput);
                    AssertNoIterationLimit(
                        last,
                        "CombatGirl.30-50.NarrowGap tick " + i
                        + " pos=" + last.XMillimeters + "," + last.YMillimeters + "," + last.ZMillimeters
                        + " flags=" + last.CollisionFlags
                        + " target=" + last.Diagnostic.TargetBodyId.Value + "." + last.Diagnostic.TargetColliderId.Value
                        + " normal=" + last.Diagnostic.Normal
                        + " requested=" + last.Diagnostic.RequestedDelta
                        + " applied=" + last.Diagnostic.AppliedDelta);
                }

                int deltaX = last.XMillimeters - startX;
                int deltaZ = last.ZMillimeters - startZ;
                long horizontalDistanceSquared = (long)deltaX * deltaX + (long)deltaZ * deltaZ;
                Assert.Greater(
                    horizontalDistanceSquared,
                    3000L * 3000L,
                    "CombatGirl must escape the original 1m 30/50 slope gap instead of freezing.");
            }

            Assert.IsFalse(scene.isDirty);
        }

        [Test]
        public void FormalCombatGirlCapsule_LiveThirtyFiftyGapPoseDoesNotIterationLimitWhileIdle()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            GameManager manager = CharacterTestSlopePlayableFixture.FindGameManager(scene);

            using (CharacterTestSlopePlayableFixture fixture =
                   CharacterTestSlopePlayableFixture.Create(manager))
            {
                fixture.Entity.SetPhysics(new CharacterTestCharacterPhysicsComponent(
                    capsuleRadiusMeters: 0.58899695f,
                    capsuleHeightMeters: 2.0562358f,
                    capsuleCenterY: 1.0281179f,
                    massKg: 80f));
                fixture.Entity.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        xMillimeters: -23482,
                        yMillimeters: 2051,
                        zMillimeters: 16661,
                        yawMilliDegrees: 0));
                fixture.Entity.SetMotionState(
                    verticalVelocity: 0f,
                    lastMoveInput: Vector2.zero,
                    isGrounded: false,
                    horizontalVelocity: Vector2.zero,
                    desiredVelocity: Vector2.zero,
                    movementMode: CharacterMovementMode.Idle,
                    physicsEnabled: true,
                    collisionFlags: CombatMotionCollisionFlags.None,
                    surfaceMaterial: CombatSurfaceMaterial.Default);

                for (int i = 0; i < 5; i++)
                {
                    CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                        0,
                        0,
                        CharacterTestRuntimeCommandSources.PlayerInput);
                    AssertNoIterationLimit(row, "CombatGirl.LiveGap.Idle tick " + i);
                }
            }

            Assert.IsFalse(scene.isDirty);
        }

        [Test]
        public void FormalCombatGirlCapsule_ActiveLocomotionSlidesAlongStraightWallAtRequestedSpeed()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            Assert.IsFalse(scene.isDirty);
            GameManager manager = CharacterTestSlopePlayableFixture.FindGameManager(scene);

            using (CharacterTestSlopePlayableFixture fixture =
                   CharacterTestSlopePlayableFixture.Create(manager))
            {
                fixture.Entity.SetPhysics(new CharacterTestCharacterPhysicsComponent(
                    capsuleRadiusMeters: 0.58899695f,
                    capsuleHeightMeters: 2.0562358f,
                    capsuleCenterY: 1.0281179f,
                    massKg: 80f));
                fixture.Entity.ClearMotionRequests();
                fixture.Entity.SetInput(CharacterTestCharacterInputComponent.None);
                fixture.Entity.SetAction(CharacterTestCharacterActionComponent.None);
                fixture.Entity.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        xMillimeters: -21901,
                        yMillimeters: 0,
                        zMillimeters: 500,
                        yawMilliDegrees: 0));
                fixture.Entity.SetMotionState(
                    verticalVelocity: 0f,
                    lastMoveInput: new Vector2(-0.70710677f, 0.70710677f),
                    isGrounded: true,
                    horizontalVelocity: new Vector2(-4.2426405f, 4.2426405f),
                    desiredVelocity: new Vector2(-4.2426405f, 4.2426405f),
                    movementMode: CharacterMovementMode.Run,
                    physicsEnabled: true,
                    collisionFlags: CombatMotionCollisionFlags.Grounded,
                    surfaceMaterial: CombatSurfaceMaterial.Default);

                int startZ = fixture.Entity.Transform.ZMillimeters;
                int previousZ = startZ;
                int wallTicks = 0;
                bool contactedWall = false;
                for (int i = 0; i < 18; i++)
                {
                    CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                        -1000,
                        1000,
                        CharacterTestRuntimeCommandSources.PlayerInput);
                    AssertNoIterationLimit(row, "CombatGirl.WallSlide tick " + i);
                    Assert.Greater(
                        row.ZMillimeters,
                        previousZ,
                        "Active locomotion stuck on the straight wall at tick " + i);
                    previousZ = row.ZMillimeters;

                    if (!HasFlag(row.CollisionFlags, CombatMotionCollisionFlags.Wall))
                        continue;
                    contactedWall = true;
                    Assert.IsFalse(
                        HasFlag(row.CollisionFlags, CombatMotionCollisionFlags.BlockedZ),
                        "The straight wall must not block its legal Z tangent at tick " + i);

                    if (i < 4 || !row.HasDiagnostic)
                        continue;
                    FixVector3 requestedHorizontal = new FixVector3(
                        row.Diagnostic.RequestedDelta.X,
                        Fix64.Zero,
                        row.Diagnostic.RequestedDelta.Z);
                    FixVector3 appliedHorizontal = new FixVector3(
                        row.Diagnostic.AppliedDelta.X,
                        Fix64.Zero,
                        row.Diagnostic.AppliedDelta.Z);
                    Fix64 requestedLength = requestedHorizontal.LengthSquared().Sqrt();
                    Fix64 appliedLength = appliedHorizontal.LengthSquared().Sqrt();
                    Assert.That(
                        appliedLength.RawValue,
                        Is.InRange(
                            requestedLength.RawValue - 2000L,
                            requestedLength.RawValue + 2000L),
                        "Active locomotion lost wall-tangent speed at tick " + i);
                    wallTicks++;
                }

                Assert.IsTrue(contactedWall, "The formal live fixture never reached Wall_X.");
                Assert.GreaterOrEqual(wallTicks, 12);
                Assert.Greater(
                    fixture.Entity.Transform.ZMillimeters - startZ,
                    900,
                    "Formal live wall slide did not retain meaningful locomotion speed.");
                Assert.Less(
                    fixture.Entity.Transform.ZMillimeters,
                    2700,
                    "The fixture must stop before the separate 0.55m step blocker at z=3.35m.");
            }

            Assert.IsFalse(scene.isDirty);
        }

        private static Scene OpenFormalSceneWithNeutralActiveDriver()
        {
            Scene formalScene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(formalScene.IsValid() && formalScene.isLoaded);

            // Guidance owns a separate formal-scene preflight and the shared worktree currently
            // contains invalid Guidance authoring. Keep the unchanged formal scene loaded so its
            // serialized root/cache/settings and Play Mode objects remain real, while an empty,
            // unsaved test-driver scene is active solely during this isolated authority test.
            Scene driverScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            if (SceneManager.GetActiveScene().handle != driverScene.handle)
            {
                Assert.IsTrue(
                    EditorSceneManager.SetActiveScene(driverScene),
                    "The neutral T6 driver scene could not become active.");
            }
            Assert.AreEqual(
                driverScene.handle,
                SceneManager.GetActiveScene().handle,
                "The neutral T6 driver scene must be active before EnterPlayMode.");
            return driverScene;
        }

        private static void CloseNeutralActiveDriver(Scene driverScene)
        {
            if (driverScene.IsValid() && driverScene.isLoaded)
                EditorSceneManager.CloseScene(driverScene, removeScene: true);
        }

        private static void AssertWalkableTravel(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            long longitudinalRaw,
            short inputAlongUphillRaw,
            int expectedYDirection,
            bool requireGroundSnap,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                longitudinalRaw,
                lateralRaw: 0,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();

            int firstY = 0;
            int lastY = 0;
            int contactCount = 0;
            bool sawSnap = false;
            bool sawExpectedAppliedY = false;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.WalkableTravelTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    inputAlongUphillRaw,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                AssertNoIterationLimit(row, context + " tick " + i);
                bool interior = fixture.IsWithinSlopeInterior(slopeBodyId, row);
                if (interior)
                {
                    Assert.IsTrue(
                        row.IsSlopeContact(slopeBodyId),
                        context + " lost slope contact inside valid support phase " + i
                        + " position=" + row.XMillimeters + "," + row.YMillimeters + "," + row.ZMillimeters
                        + " grounded=" + row.Grounded
                        + " flags=" + row.CollisionFlags
                        + " hasCollision=" + row.Diagnostic.HasCollision
                        + " target=" + row.Diagnostic.TargetBodyId.Value + "." + row.Diagnostic.TargetColliderId.Value
                        + " classification=" + row.Diagnostic.SurfaceClassification
                        + " diagnosticFlags=" + row.Diagnostic.CollisionFlags
                        + " distanceRaw=" + row.Diagnostic.CollisionDistance.RawValue
                        + " fractionRaw=" + row.Diagnostic.CollisionFraction.RawValue
                        + " requested=" + row.Diagnostic.RequestedDelta
                        + " applied=" + row.Diagnostic.AppliedDelta
                        + " snapRaw=" + row.Diagnostic.GroundSnapAppliedDistance.RawValue);
                    AssertWalkableSlopeContact(row, slopeBodyId, context + " phase " + i);
                }
                if (!interior)
                    continue;
                if (contactCount == 0)
                    firstY = row.YMillimeters;
                lastY = row.YMillimeters;
                contactCount++;
                sawSnap |= HasFlag(
                    row.Diagnostic.CollisionFlags,
                    CombatMotionCollisionFlags.GroundSnap);
                if (expectedYDirection > 0)
                    sawExpectedAppliedY |= row.Diagnostic.AppliedDelta.Y > Fix64.Zero;
                else
                    sawExpectedAppliedY |= row.Diagnostic.AppliedDelta.Y < Fix64.Zero;
            }

            Assert.GreaterOrEqual(contactCount, 30, context + " slope contact window");
            Assert.IsTrue(sawExpectedAppliedY, context + " expected applied Y direction");
            if (expectedYDirection > 0)
                Assert.Greater(lastY - firstY, 200, context + " uphill authority Y");
            else
                Assert.Less(lastY - firstY, -200, context + " downhill authority Y");
            if (requireGroundSnap)
                Assert.IsTrue(sawSnap, context + " should observe GroundSnap while descending");

            IReadOnlyList<CharacterTestSlopeAuthorityRow> seamRows =
                CaptureFormalSlopeSeamRows(
                    fixture,
                    slopeBodyId,
                    inputAlongUphillRaw,
                    context + ".Seam");
            AssertFormalSlopeSeamWindow(fixture, slopeBodyId, seamRows, context + ".Seam");
        }

        private static IReadOnlyList<CharacterTestSlopeAuthorityRow> CaptureFormalSlopeSeamRows(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            short directedInputRaw,
            string context)
        {
            const int seamTicks = 96;
            const long startInsideEndpointRaw = 1000000L;
            int directionSign = Math.Sign(directedInputRaw);
            Assert.AreNotEqual(0, directionSign, context + " directed input");
            long halfLengthRaw = fixture.GetSlopeLongitudinalHalfExtentRaw(slopeBodyId);
            fixture.PlaceOnSlope(
                slopeBodyId,
                directionSign * (halfLengthRaw - startInsideEndpointRaw),
                lateralRaw: 0,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();

            var rows = new List<CharacterTestSlopeAuthorityRow>(seamTicks);
            for (int i = 0; i < seamTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    directedInputRaw,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                AssertNoIterationLimit(row, context + " dedicated tick " + i);
                rows.Add(row);
            }
            return rows;
        }

        private static void AssertFormalSlopeSeamWindow(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            IReadOnlyList<CharacterTestSlopeAuthorityRow> rows,
            string context)
        {
            const int beforeTicks = 8;
            const int afterTicks = 30;
            const int seamHeightToleranceMillimeters = 20;
            const long seamNonEnteringToleranceRaw2 = 2000000000L;
            Assert.Greater(rows.Count, beforeTicks + afterTicks, context + " row count");

            long firstLongitudinal = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[0]);
            long lastLongitudinal = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[rows.Count - 1]);
            int directionSign = Math.Sign(lastLongitudinal - firstLongitudinal);
            Assert.AreNotEqual(0, directionSign, context + " directed travel");

            long halfLengthRaw = fixture.GetSlopeLongitudinalHalfExtentRaw(slopeBodyId);
            int crossingIndex = -1;
            for (int i = 1; i < rows.Count; i++)
            {
                long previous = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[i - 1]);
                long current = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[i]);
                if (directionSign * previous < halfLengthRaw
                    && directionSign * current >= halfLengthRaw)
                {
                    crossingIndex = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(
                crossingIndex,
                beforeTicks,
                context + " must cross the formal OBB endpoint with a complete pre-window");
            Assert.Less(
                crossingIndex + afterTicks,
                rows.Count,
                context + " must cross the formal OBB endpoint with a complete post-window");

            bool hasAdjacentSupport = fixture.TryResolveAdjacentSeamAabb(
                slopeBodyId,
                directionSign,
                out CombatBodyId adjacentBodyId,
                out CombatColliderId adjacentColliderId,
                out int adjacentSurfaceYMillimeters);
            int windowStart = crossingIndex - beforeTicks;
            int windowEnd = crossingIndex + afterTicks;
            long startLongitudinal = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[windowStart]);
            long endLongitudinal = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[windowEnd]);
            Assert.Greater(
                directionSign * (endLongitudinal - startLongitudinal),
                200000L,
                context + " must make more than 0.20m authority progress through the seam window");

            int crossingY = rows[crossingIndex].YMillimeters;
            int departureReferenceY = crossingY;
            bool sawAdjacentSupport = false;
            bool sawAirborne = false;
            bool sawLowerLanding = false;
            for (int i = windowStart; i <= windowEnd; i++)
            {
                CharacterTestSlopeAuthorityRow row = rows[i];
                string tickContext = context
                    + " tick=" + i
                    + " position=" + row.XMillimeters + "," + row.YMillimeters + "," + row.ZMillimeters
                    + " grounded=" + row.Grounded
                    + " flags=" + row.CollisionFlags
                    + " contact=" + (row.HasDiagnostic && row.Diagnostic.HasCollision
                        ? row.Diagnostic.TargetBodyId.Value + "." + row.Diagnostic.TargetColliderId.Value
                            + "/" + row.Diagnostic.TargetShapeKind
                            + "/" + row.Diagnostic.SurfaceClassification
                            + "/" + row.Diagnostic.CollisionFlags
                        : "none");
                AssertNoIterationLimit(row, tickContext);

                if (i > windowStart)
                {
                    long previous = fixture.GetSlopeLongitudinalRaw(slopeBodyId, rows[i - 1]);
                    long current = fixture.GetSlopeLongitudinalRaw(slopeBodyId, row);
                    Assert.GreaterOrEqual(
                        directionSign * (current - previous),
                        -2000L,
                        tickContext + " must not reverse or stick at the seam");
                }

                bool onSlopeContact = row.IsSlopeContact(slopeBodyId);
                if (onSlopeContact)
                {
                    if (HasFlag(
                            row.Diagnostic.CollisionFlags,
                            CombatMotionCollisionFlags.GroundSnap))
                    {
                        Assert.That(
                            row.Diagnostic.GroundSnapAppliedDistance.RawValue,
                            Is.InRange(0L, 100000L),
                            tickContext + " slope GroundSnap must remain within the formal 0.10m bound");
                    }
                    else
                    {
                        Assert.IsTrue(
                            CombatKinematicMotor.TryRawDotNumerator(
                                row.Diagnostic.AppliedDelta,
                                row.Diagnostic.Normal,
                                out long appliedDotNormalRaw2),
                            tickContext + " slope applied-dot calculation");
                        Assert.GreaterOrEqual(
                            appliedDotNormalRaw2,
                            -seamNonEnteringToleranceRaw2,
                            tickContext
                            + " slope applied delta must remain within the 2mm authority-roundtrip tolerance");
                    }
                    if (i >= crossingIndex)
                        departureReferenceY = row.YMillimeters;
                }

                if (i < crossingIndex)
                    Assert.IsTrue(row.Grounded, tickContext + " pre-seam support must remain grounded");

                if (i >= crossingIndex && !onSlopeContact)
                {
                    Assert.LessOrEqual(
                        row.VerticalVelocity,
                        0,
                        tickContext + " seam traversal must not create an upward bounce");
                    Assert.LessOrEqual(
                        row.YMillimeters,
                        departureReferenceY + seamHeightToleranceMillimeters,
                        tickContext + " seam traversal must not pop upward");
                }

                bool onAdjacentSupport = row.HasDiagnostic
                    && row.Diagnostic.HasCollision
                    && row.Diagnostic.TargetShapeKind == CombatPhysicsShapeKind.Aabb
                    && row.Diagnostic.TargetBodyId.Equals(adjacentBodyId)
                    && row.Diagnostic.TargetColliderId.Equals(adjacentColliderId);
                if (hasAdjacentSupport)
                {
                    Assert.IsTrue(row.Grounded, tickContext + " matched formal adjacent ground must not flicker");
                    if (!onAdjacentSupport)
                        continue;

                    sawAdjacentSupport = true;
                    Assert.AreEqual(
                        CharacterTestMotionSurfaceClassification.Walkable,
                        row.Diagnostic.SurfaceClassification,
                        tickContext + " adjacent ground classification");
                    Assert.That(
                        row.YMillimeters,
                        Is.InRange(adjacentSurfaceYMillimeters - 20, adjacentSurfaceYMillimeters + 20),
                        tickContext + " adjacent ground feet height must not penetrate or float");
                }
                else if (i >= crossingIndex && !onSlopeContact)
                {
                    if (!row.Grounded)
                    {
                        Assert.IsFalse(
                            sawLowerLanding,
                            tickContext + " must not become airborne again after the lower landing");
                        sawAirborne = true;
                    }
                    else if (sawAirborne)
                    {
                        Assert.IsTrue(
                            row.HasDiagnostic
                            && row.Diagnostic.HasCollision
                            && row.Diagnostic.TargetShapeKind == CombatPhysicsShapeKind.Aabb
                            && row.Diagnostic.SurfaceClassification
                                == CharacterTestMotionSurfaceClassification.Walkable,
                            tickContext + " post-airborne support must be a typed walkable AABB");
                        Assert.LessOrEqual(
                            row.YMillimeters,
                            departureReferenceY - 100,
                            tickContext + " post-airborne landing must be meaningfully below the unsupported edge");
                        sawLowerLanding = true;
                    }
                    else
                    {
                        Assert.Fail(
                            tickContext + " unsupported upper edge became grounded before an airborne phase");
                    }
                }
            }

            if (hasAdjacentSupport)
            {
                Assert.IsTrue(
                    sawAdjacentSupport,
                    context + " must hand authority contact from the slope to the matched formal AABB");
            }
            else
            {
                Assert.IsTrue(
                    sawAirborne,
                    context + " formal upper edge has no height-matched AABB and must transition once to airborne");
            }
        }

        private static void AssertWalkableTraverse(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                longitudinalRaw: 0,
                CharacterTestSlopePlayableFixture.TraverseStartLateralRaw,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();

            int firstY = 0;
            int lastY = 0;
            int firstZ = 0;
            int lastZ = 0;
            int contacts = 0;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.WalkableTravelTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    0,
                    CharacterTestSlopePlayableFixture.WalkableTraverseInputRaw,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                AssertNoIterationLimit(row, context + " tick " + i);
                bool interior = fixture.IsWithinSlopeInterior(slopeBodyId, row);
                if (interior)
                {
                    Assert.IsTrue(
                        row.IsSlopeContact(slopeBodyId),
                        context + " lost slope contact inside valid support phase " + i);
                    AssertWalkableSlopeContact(row, slopeBodyId, context + " phase " + i);
                }
                if (!interior)
                    continue;
                if (contacts == 0)
                {
                    firstY = row.YMillimeters;
                    firstZ = row.ZMillimeters;
                }
                lastY = row.YMillimeters;
                lastZ = row.ZMillimeters;
                contacts++;
            }

            Assert.GreaterOrEqual(
                contacts,
                20,
                context + " valid support phase before completing the lateral crossing");
            Assert.LessOrEqual(Math.Abs(lastY - firstY), 10, context + " unrelated Y drift");
            Assert.Greater(Math.Abs(lastZ - firstZ), 200, context + " traverse authority Z");
        }

        private static void AssertIdleSixHundred(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                longitudinalRaw: 0,
                lateralRaw: 0,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();
            int startX = fixture.Entity.Transform.XMillimeters;
            int startZ = fixture.Entity.Transform.ZMillimeters;
            int groundedCount = 0;
            int slopeContactCount = 0;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.IdleTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    0,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                groundedCount += row.Grounded ? 1 : 0;
                slopeContactCount += row.IsSlopeContact(slopeBodyId) ? 1 : 0;
                Assert.AreEqual(startX, row.XMillimeters, context + " X " + i);
                Assert.AreEqual(startZ, row.ZMillimeters, context + " Z " + i);
                Assert.AreEqual(Fix64.Zero, row.Diagnostic.RequestedDelta.X, context + " requested X " + i);
                Assert.AreEqual(Fix64.Zero, row.Diagnostic.RequestedDelta.Z, context + " requested Z " + i);
                Assert.AreEqual(0, row.DesiredVelocityX, context + " desired X " + i);
                Assert.AreEqual(0, row.DesiredVelocityY, context + " desired Z " + i);
                AssertNoIterationLimit(row, context + " tick " + i);
            }

            Assert.AreEqual(CharacterTestSlopePlayableFixture.IdleTicks, groundedCount, context + " grounded ticks");
            Assert.GreaterOrEqual(slopeContactCount, 590, context + " slope contact ticks");
        }

        private static void AssertJumpAndLanding(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                longitudinalRaw: 0,
                lateralRaw: 0,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();

            CharacterTestSlopeAuthorityRow takeoff = fixture.TickControl(
                0,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput,
                jumpPressed: true,
                jumpHeld: true);
            Assert.IsFalse(
                takeoff.HasDiagnostic
                && HasFlag(takeoff.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.GroundSnap),
                context + " jump tick must suppress GroundSnap");
            Assert.Greater(takeoff.VerticalVelocity, 0, context + " takeoff vertical velocity");

            bool sawAirborne = !takeoff.Grounded;
            bool landed = false;
            int landingTick = -1;
            for (int i = 1; i < CharacterTestSlopePlayableFixture.JumpLandMaxTicks; i++)
            {
                bool held = i < CharacterTestSlopePlayableFixture.JumpHeldTicks;
                bool released = i == CharacterTestSlopePlayableFixture.JumpHeldTicks;
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    0,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    jumpPressed: false,
                    jumpHeld: held,
                    jumpReleased: released);
                sawAirborne |= !row.Grounded;
                if (sawAirborne
                    && row.Grounded
                    && row.IsSlopeContact(slopeBodyId))
                {
                    landed = true;
                    landingTick = i;
                    break;
                }
            }

            Assert.IsTrue(sawAirborne, context + " should leave the slope");
            Assert.IsTrue(landed, context + " should land back on the same slope");
            Assert.Less(landingTick, CharacterTestSlopePlayableFixture.JumpLandMaxTicks, context + " landing tick");
        }

        private static void AssertEdgeLeave(CharacterTestSlopePlayableFixture fixture)
        {
            const string context = "30.Edge";
            fixture.PlaceOnSlope(
                CharacterTestSlopePlayableFixture.Slope30BodyId,
                longitudinalRaw: 0,
                CharacterTestSlopePlayableFixture.EdgeStartLateralRaw,
                grounded: true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            fixture.SettleWalkable();

            bool crossedGuard = false;
            bool sawAirborneAfterGuard = false;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.EdgeTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    0,
                    CharacterTestSlopePlayableFixture.EdgeInputRaw,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                int edgeGap = fixture.ComputeHorizontalEdgeGapMillimeters(
                    CharacterTestSlopePlayableFixture.Slope30BodyId,
                    row);
                if (edgeGap <= CharacterTestSlopePlayableFixture.EdgeGuardMillimeters)
                    continue;

                crossedGuard = true;
                bool slopeSnap = row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope30BodyId)
                    && HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.GroundSnap);
                Assert.IsFalse(slopeSnap, context + " slope snap after gap tick " + i);
                sawAirborneAfterGuard |= !row.Grounded;
            }

            Assert.IsTrue(crossedGuard, context + " should exceed the 0.10m support guard");
            Assert.IsTrue(sawAirborneAfterGuard, context + " should become airborne before any later grid landing");
        }

        private static void AssertSteepControl(
            CharacterTestSlopePlayableFixture fixture,
            int commandSourceId,
            short worldMoveXRaw,
            short worldMoveZRaw,
            string context)
        {
            fixture.PlaceOnSlope(
                CharacterTestSlopePlayableFixture.Slope50BodyId,
                CharacterTestSlopePlayableFixture.SteepStartLongitudinalRaw,
                lateralRaw: 0,
                grounded: false,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            int startY = fixture.Entity.Transform.YMillimeters;
            int startZ = fixture.Entity.Transform.ZMillimeters;
            int maximumY = startY;
            int steepContacts = 0;
            bool sawExpectedSource = false;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.SteepTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    worldMoveXRaw,
                    worldMoveZRaw,
                    commandSourceId,
                    worldMoveOnly: commandSourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner);
                maximumY = Math.Max(maximumY, row.YMillimeters);
                sawExpectedSource |= row.ControlIntentSourceId == commandSourceId;
                AssertNoIterationLimit(row, context + " tick " + i);
                if (!row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId))
                    continue;
                steepContacts++;
                AssertSteepSlopeContact(row, context + " phase " + i);
                Assert.LessOrEqual(
                    row.Diagnostic.AppliedDelta.Y.RawValue,
                    0L,
                    context + " no positive steep applied Y " + i);
            }

            Assert.GreaterOrEqual(steepContacts, 1, context + " steep contact phase");
            Assert.IsTrue(sawExpectedSource, context + " typed command source");
            Assert.LessOrEqual(
                maximumY - startY,
                CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters,
                context + " authority maximum rise");
            if (worldMoveZRaw != 0)
                Assert.Greater(Math.Abs(fixture.Entity.Transform.ZMillimeters - startZ), 50, context + " traverse Z");
        }

        private static void AssertDashStrike(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            bool expectWalkableAscent,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                CharacterTestSlopePlayableFixture.DashStartLongitudinalRaw,
                lateralRaw: 0,
                grounded: expectWalkableAscent,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            if (expectWalkableAscent)
                fixture.SettleWalkable();
            int startY = fixture.Entity.Transform.YMillimeters;
            int maximumY = startY;
            bool assigned = fixture.AssignDashStrikeSlot();
            Assert.IsTrue(assigned, context + " hotbar slot assignment");

            CharacterTestSlopeAuthorityRow[] rows = fixture.RunDashStrike();
            bool sawAction = false;
            bool sawRequestedRootMotion = false;
            bool sawExpectedSlope = false;
            bool sawSteep = false;
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestSlopeAuthorityRow row = rows[i];
                maximumY = Math.Max(maximumY, row.YMillimeters);
                sawAction |= row.ActiveActionId == CharacterTestSlopePlayableFixture.DashStrikeActionId;
                sawRequestedRootMotion |= row.HasDiagnostic
                    && row.Diagnostic.RequestedDelta.X < Fix64.Zero;
                sawExpectedSlope |= row.IsSlopeContact(slopeBodyId);
                sawSteep |= row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId)
                    && row.Diagnostic.SurfaceClassification == CharacterTestMotionSurfaceClassification.Steep;
                AssertNoIterationLimit(row, context + " tick " + i);
            }

            Assert.IsTrue(sawAction, context + " must start formal action 327");
            Assert.IsTrue(sawRequestedRootMotion, context + " must emit authored Root Motion");
            Assert.IsTrue(sawExpectedSlope, context + " slope contact");
            if (expectWalkableAscent)
            {
                Assert.Greater(maximumY - startY, 200, context + " walkable ascent");
            }
            else
            {
                Assert.IsTrue(sawSteep, context + " steep contact");
                Assert.LessOrEqual(
                    maximumY - startY,
                    CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters,
                    context + " blocked maximum rise");
            }
        }

        private static void AssertImpulse(
            CharacterTestSlopePlayableFixture fixture,
            int slopeBodyId,
            bool expectWalkableAscent,
            string context)
        {
            fixture.PlaceOnSlope(
                slopeBodyId,
                CharacterTestSlopePlayableFixture.ImpulseStartLongitudinalRaw,
                lateralRaw: 0,
                grounded: expectWalkableAscent,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            if (expectWalkableAscent)
                fixture.SettleWalkable();
            int startY = fixture.Entity.Transform.YMillimeters;
            int maximumY = startY;
            fixture.EnqueueFormalImpulse();
            bool sawSlope = false;
            bool sawSteep = false;
            bool sawRequestedImpulse = false;
            bool sawRequestWithAppliedOrResolvedMotion = false;
            for (int i = 0; i < CharacterTestSlopePlayableFixture.ImpulseTicks; i++)
            {
                CharacterTestSlopeAuthorityRow row = fixture.TickControl(
                    0,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput);
                maximumY = Math.Max(maximumY, row.YMillimeters);
                sawSlope |= row.IsSlopeContact(slopeBodyId);
                sawSteep |= row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId)
                    && row.Diagnostic.SurfaceClassification == CharacterTestMotionSurfaceClassification.Steep;
                if (row.HasDiagnostic
                    && row.Diagnostic.RequestedDelta.X < Fix64.Zero
                    && row.Diagnostic.RequestedDelta.Z > Fix64.Zero)
                {
                    sawRequestedImpulse = true;
                    sawRequestWithAppliedOrResolvedMotion |= row.Diagnostic.AppliedDelta.X != Fix64.Zero
                        || row.Diagnostic.AppliedDelta.Z != Fix64.Zero
                        || row.Diagnostic.ResolvedVelocity.X != Fix64.Zero
                        || row.Diagnostic.ResolvedVelocity.Z != Fix64.Zero;
                }
                if (row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId))
                {
                    Assert.IsTrue(
                        CombatKinematicMotor.TryRawDotNumerator(
                            row.Diagnostic.AppliedDelta,
                            row.Diagnostic.Normal,
                            out long appliedDotNormalRaw2),
                        context + " steep applied-dot tick " + i);
                    Assert.GreaterOrEqual(
                        appliedDotNormalRaw2,
                        0L,
                        context + " impulse result must not enter the 50-degree OBB tick " + i);
                }
                AssertNoIterationLimit(row, context + " tick " + i);
            }

            Assert.IsTrue(sawSlope, context + " slope contact");
            Assert.IsTrue(sawRequestedImpulse, context + " must consume the authored nonzero Impulse request");
            Assert.IsTrue(
                sawRequestWithAppliedOrResolvedMotion,
                context + " request tick must retain allowed applied or resolved horizontal motion");
            if (expectWalkableAscent)
            {
                Assert.Greater(maximumY - startY, 10, context + " impulse walkable ascent");
            }
            else
            {
                Assert.IsTrue(sawSteep, context + " steep contact");
                Assert.LessOrEqual(
                    maximumY - startY,
                    CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters,
                    context + " impulse blocked maximum rise");
            }
        }

        private static void AssertRowsEquivalent(
            CharacterTestSlopeAuthorityRow[] expected,
            CharacterTestSlopeAuthorityRow[] actual,
            string context)
        {
            Assert.AreEqual(expected.Length, actual.Length, context + " length");
            long expectedStartFrame = expected.Length > 0 ? expected[0].FrameValue : 0L;
            long actualStartFrame = actual.Length > 0 ? actual[0].FrameValue : 0L;
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i].AssertEquivalent(
                    actual[i],
                    expectedStartFrame,
                    actualStartFrame,
                    context + " tick " + i);
            }
        }

        private static void AssertRowEquivalent(
            CharacterTestSlopeAuthorityRow expected,
            CharacterTestSlopeAuthorityRow actual,
            string context)
        {
            expected.AssertEquivalent(
                actual,
                expected.FrameValue,
                actual.FrameValue,
                context);
        }

        private static void AssertWalkableSlopeContact(
            CharacterTestSlopeAuthorityRow row,
            int slopeBodyId,
            string context)
        {
            AssertSlopeContactIdentity(row, slopeBodyId, context);
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Walkable,
                row.Diagnostic.SurfaceClassification,
                context + " classification");
            Assert.IsTrue(row.Grounded, context + " aggregate grounded");
            Assert.IsTrue(row.Diagnostic.Grounded, context + " diagnostic grounded");
            Assert.IsTrue(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Grounded),
                context + " grounded flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.SteepSlope),
                context + " no steep flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Wall),
                context + " no wall flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Ceiling),
                context + " no ceiling flag");
            Assert.AreEqual(CombatSurfaceMaterial.Rough, row.SurfaceMaterial, context + " aggregate material");
        }

        private static void AssertSteepSlopeContact(
            CharacterTestSlopeAuthorityRow row,
            string context)
        {
            AssertSlopeContactIdentity(
                row,
                CharacterTestSlopePlayableFixture.Slope50BodyId,
                context);
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Steep,
                row.Diagnostic.SurfaceClassification,
                context + " classification");
            Assert.IsTrue(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.SteepSlope),
                context + " steep flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Grounded),
                context + " selected collision must not be Grounded");
            Assert.IsFalse(row.Diagnostic.Grounded, context + " selected diagnostic grounded");
            Assert.IsTrue(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Wall),
                context + " steep surfaces retain the M2 Wall flag");
            Assert.IsTrue(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.BlockedX),
                context + " steep X block flag");
            Assert.IsTrue(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.BlockedY),
                context + " steep Y block flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.BlockedZ),
                context + " no steep Z block flag");
            Assert.IsFalse(
                HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.Ceiling),
                context + " no ceiling flag");
            Assert.Less(
                row.Diagnostic.Normal.Y.RawValue,
                row.Diagnostic.GroundMinNormalY.RawValue,
                context + " normal below threshold");
        }

        private static void AssertSlopeContactIdentity(
            CharacterTestSlopeAuthorityRow row,
            int slopeBodyId,
            string context)
        {
            Assert.IsTrue(row.HasDiagnostic, context + " diagnostic presence");
            Assert.IsTrue(row.Diagnostic.HasCollision, context + " collision presence");
            Assert.AreEqual(slopeBodyId, row.Diagnostic.TargetBodyId.Value, context + " body");
            Assert.AreEqual(
                CharacterTestSlopePlayableFixture.SlopeColliderId,
                row.Diagnostic.TargetColliderId.Value,
                context + " collider");
            Assert.AreEqual(
                CombatPhysicsShapeKind.Obb,
                row.Diagnostic.TargetShapeKind,
                context + " shape");
            CharacterTestSlopePlayableFixture.AssertSlopeNormalWithinTolerance(
                CharacterTestSlopePlayableFixture.ExpectedSlopeNormal(slopeBodyId),
                row.Diagnostic.Normal,
                context + " normal");
            Assert.AreEqual(
                CharacterTestSlopePlayableFixture.ExpectedGroundMinNormalYRaw,
                row.Diagnostic.GroundMinNormalY.RawValue,
                context + " threshold");
            Assert.AreEqual(
                CombatSurfaceMaterial.Rough,
                row.Diagnostic.SurfaceMaterial,
                context + " material");
            AssertNoIterationLimit(row, context);
        }

        private static void AssertNoIterationLimit(CharacterTestSlopeAuthorityRow row, string context)
        {
            Assert.IsFalse(
                HasFlag(row.CollisionFlags, CombatMotionCollisionFlags.IterationLimit),
                context + " aggregate IterationLimit");
            Assert.IsFalse(
                row.HasDiagnostic
                && HasFlag(row.Diagnostic.CollisionFlags, CombatMotionCollisionFlags.IterationLimit),
                context + " diagnostic IterationLimit");
        }

        private static bool HasFlag(
            CombatMotionCollisionFlags value,
            CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }
    }

    internal enum CharacterTestSlopeNoAllocCase
    {
        ThirtyUphill = 0,
        ThirtyDownhillSnap = 1,
        ThirtyTraverse = 2,
        ThirtyIdle = 3,
        FiftyBlocked = 4,
        EdgeSupportMiss = 5,
        RootMotionStaticOnly = 6,
        ImpulseRaw = 7
    }

    internal readonly struct CharacterTestSlopeSemanticScalars
    {
        public CharacterTestSlopeSemanticScalars(
            int tickCount,
            int groundedCount,
            int slopeContactCount,
            int expectedSlopeContactCount,
            int expectedSlopeInteriorCount,
            int steepCount,
            int snapCount,
            int edgeBeyondGuardCount,
            int edgeAirborneCount,
            int edgeSlopeSnapCount,
            int nonZeroRequestedXCount,
            int nonZeroAppliedOrResolvedXCount,
            int startX,
            int startY,
            int startZ,
            int finalX,
            int finalY,
            int finalZ,
            int maximumY,
            bool sawAirborne,
            bool commandsAccepted)
        {
            TickCount = tickCount;
            GroundedCount = groundedCount;
            SlopeContactCount = slopeContactCount;
            ExpectedSlopeContactCount = expectedSlopeContactCount;
            ExpectedSlopeInteriorCount = expectedSlopeInteriorCount;
            SteepCount = steepCount;
            SnapCount = snapCount;
            EdgeBeyondGuardCount = edgeBeyondGuardCount;
            EdgeAirborneCount = edgeAirborneCount;
            EdgeSlopeSnapCount = edgeSlopeSnapCount;
            NonZeroRequestedXCount = nonZeroRequestedXCount;
            NonZeroAppliedOrResolvedXCount = nonZeroAppliedOrResolvedXCount;
            StartX = startX;
            StartY = startY;
            StartZ = startZ;
            FinalX = finalX;
            FinalY = finalY;
            FinalZ = finalZ;
            MaximumY = maximumY;
            SawAirborne = sawAirborne;
            CommandsAccepted = commandsAccepted;
        }

        public int TickCount { get; }
        public int GroundedCount { get; }
        public int SlopeContactCount { get; }
        public int ExpectedSlopeContactCount { get; }
        public int ExpectedSlopeInteriorCount { get; }
        public int SteepCount { get; }
        public int SnapCount { get; }
        public int EdgeBeyondGuardCount { get; }
        public int EdgeAirborneCount { get; }
        public int EdgeSlopeSnapCount { get; }
        public int NonZeroRequestedXCount { get; }
        public int NonZeroAppliedOrResolvedXCount { get; }
        public int StartX { get; }
        public int StartY { get; }
        public int StartZ { get; }
        public int FinalX { get; }
        public int FinalY { get; }
        public int FinalZ { get; }
        public int MaximumY { get; }
        public bool SawAirborne { get; }
        public bool CommandsAccepted { get; }
    }

    internal readonly struct CharacterTestSlopeAuthorityRow
    {
        public CharacterTestSlopeAuthorityRow(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            FrameValue = frame.Value;
            XMillimeters = entity.Transform.XMillimeters;
            YMillimeters = entity.Transform.YMillimeters;
            ZMillimeters = entity.Transform.ZMillimeters;
            YawMilliDegrees = entity.Transform.YawMilliDegrees;
            VerticalVelocity = entity.VerticalVelocityMillimetersPerSecond;
            HorizontalVelocityX = entity.HorizontalVelocityXMillimetersPerSecond;
            HorizontalVelocityY = entity.HorizontalVelocityYMillimetersPerSecond;
            DesiredVelocityX = entity.DesiredVelocityXMillimetersPerSecond;
            DesiredVelocityY = entity.DesiredVelocityYMillimetersPerSecond;
            Grounded = entity.IsGrounded;
            CollisionFlags = entity.CollisionFlags;
            SurfaceMaterial = entity.SurfaceMaterial;
            SurfaceBounceSpeedCapY = entity.SurfaceBounceSpeedCapYMillimetersPerSecond;
            ControlIntentSourceId = entity.Input.ControlIntentSourceId;
            ActiveActionId = entity.Action.ActiveActionId.Value;
            HasDiagnostic = hasDiagnostic;
            Diagnostic = diagnostic;
        }

        public long FrameValue { get; }
        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }
        public int YawMilliDegrees { get; }
        public int VerticalVelocity { get; }
        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int DesiredVelocityX { get; }
        public int DesiredVelocityY { get; }
        public bool Grounded { get; }
        public CombatMotionCollisionFlags CollisionFlags { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int SurfaceBounceSpeedCapY { get; }
        public int ControlIntentSourceId { get; }
        public int ActiveActionId { get; }
        public bool HasDiagnostic { get; }
        public CharacterTestCharacterMotionDiagnostic Diagnostic { get; }

        public bool IsSlopeContact(int bodyId)
        {
            return HasDiagnostic
                && Diagnostic.HasCollision
                && Diagnostic.TargetBodyId.Value == bodyId
                && Diagnostic.TargetShapeKind == CombatPhysicsShapeKind.Obb;
        }

        public void AssertEquivalent(
            CharacterTestSlopeAuthorityRow actual,
            long expectedStartFrame,
            long actualStartFrame,
            string context)
        {
            Assert.AreEqual(FrameValue - expectedStartFrame, actual.FrameValue - actualStartFrame, context + " frame");
            Assert.AreEqual(XMillimeters, actual.XMillimeters, context + " X");
            Assert.AreEqual(YMillimeters, actual.YMillimeters, context + " Y");
            Assert.AreEqual(ZMillimeters, actual.ZMillimeters, context + " Z");
            Assert.AreEqual(YawMilliDegrees, actual.YawMilliDegrees, context + " yaw");
            Assert.AreEqual(VerticalVelocity, actual.VerticalVelocity, context + " vertical velocity");
            Assert.AreEqual(HorizontalVelocityX, actual.HorizontalVelocityX, context + " horizontal X");
            Assert.AreEqual(HorizontalVelocityY, actual.HorizontalVelocityY, context + " horizontal Z");
            Assert.AreEqual(DesiredVelocityX, actual.DesiredVelocityX, context + " desired X");
            Assert.AreEqual(DesiredVelocityY, actual.DesiredVelocityY, context + " desired Z");
            Assert.AreEqual(Grounded, actual.Grounded, context + " grounded");
            Assert.AreEqual(CollisionFlags, actual.CollisionFlags, context + " flags");
            Assert.AreEqual(SurfaceMaterial, actual.SurfaceMaterial, context + " material");
            Assert.AreEqual(SurfaceBounceSpeedCapY, actual.SurfaceBounceSpeedCapY, context + " bounce cap");
            Assert.AreEqual(ControlIntentSourceId, actual.ControlIntentSourceId, context + " command source");
            Assert.AreEqual(ActiveActionId, actual.ActiveActionId, context + " action");
            Assert.AreEqual(HasDiagnostic, actual.HasDiagnostic, context + " diagnostic presence");
            if (!HasDiagnostic)
                return;

            Assert.AreEqual(
                Diagnostic.Frame.Value - expectedStartFrame,
                actual.Diagnostic.Frame.Value - actualStartFrame,
                context + " diagnostic frame");
            Assert.AreEqual(Diagnostic.EntityId, actual.Diagnostic.EntityId, context + " diagnostic entity");
            Assert.AreEqual(Diagnostic.HasPhysics, actual.Diagnostic.HasPhysics, context + " diagnostic physics");
            Assert.AreEqual(Diagnostic.HasCollision, actual.Diagnostic.HasCollision, context + " diagnostic collision");
            Assert.AreEqual(Diagnostic.TargetEntityId, actual.Diagnostic.TargetEntityId, context + " target entity");
            Assert.AreEqual(Diagnostic.TargetBodyId, actual.Diagnostic.TargetBodyId, context + " target body");
            Assert.AreEqual(Diagnostic.TargetColliderId, actual.Diagnostic.TargetColliderId, context + " target collider");
            Assert.AreEqual(Diagnostic.TargetShapeKind, actual.Diagnostic.TargetShapeKind, context + " target shape");
            Assert.AreEqual(Diagnostic.Normal, actual.Diagnostic.Normal, context + " normal");
            Assert.AreEqual(Diagnostic.CollisionFlags, actual.Diagnostic.CollisionFlags, context + " diagnostic flags");
            Assert.AreEqual(Diagnostic.CollisionDistance, actual.Diagnostic.CollisionDistance, context + " distance");
            Assert.AreEqual(Diagnostic.CollisionFraction, actual.Diagnostic.CollisionFraction, context + " fraction");
            Assert.AreEqual(Diagnostic.SurfaceMaterial, actual.Diagnostic.SurfaceMaterial, context + " diagnostic material");
            Assert.AreEqual(Diagnostic.GroundMinNormalY, actual.Diagnostic.GroundMinNormalY, context + " threshold");
            Assert.AreEqual(Diagnostic.SurfaceClassification, actual.Diagnostic.SurfaceClassification, context + " classification");
            Assert.AreEqual(Diagnostic.RequestedDelta, actual.Diagnostic.RequestedDelta, context + " requested");
            Assert.AreEqual(Diagnostic.AppliedDelta, actual.Diagnostic.AppliedDelta, context + " applied");
            Assert.AreEqual(Diagnostic.ResolvedVelocity, actual.Diagnostic.ResolvedVelocity, context + " resolved");
            Assert.AreEqual(Diagnostic.Grounded, actual.Diagnostic.Grounded, context + " diagnostic grounded");
            Assert.AreEqual(Diagnostic.GroundSnapAppliedDistance, actual.Diagnostic.GroundSnapAppliedDistance, context + " snap");
        }
    }

    internal sealed class CharacterTestSlopePlayableFixture : IDisposable
    {
        internal const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";
        internal const ulong ExpectedLevelHash = 12242125253111897828UL;
        internal const int Slope15BodyId = 10080;
        internal const int Slope30BodyId = 10081;
        internal const int Slope50BodyId = 10082;
        internal const int SlopeColliderId = 1;
        internal const int DashStrikeAbilityId = 910023;
        internal const int DashStrikeActionId = 327;
        internal const int DashStrikeSlot = 9;
        internal const int UphillYawMilliDegrees = 270000;
        internal const int WalkableTravelTicks = 180;
        internal const int IdleTicks = 600;
        internal const int SteepTicks = 120;
        internal const int JumpLandMaxTicks = 240;
        internal const int JumpHeldTicks = 10;
        internal const int EdgeTicks = 180;
        internal const int DashTicks = 90;
        internal const int ImpulseTicks = 120;
        internal const int EdgeGuardMillimeters = 100;
        internal const int SteepMaximumRiseMillimeters = 10;
        internal const long SlopeNormalToleranceRaw = 4L;
        internal const long ExpectedGroundMinNormalYRaw = 707128;
        internal const long UphillStartLongitudinalRaw = 2000000;
        internal const long DownhillStartLongitudinalRaw = -2000000;
        internal const long TraverseStartLateralRaw = 300000;
        internal const long EdgeStartLateralRaw = 550000;
        internal const long SteepStartLongitudinalRaw = 500000;
        internal const long DashStartLongitudinalRaw = 2200000;
        internal const long ImpulseStartLongitudinalRaw = 1200000;
        internal const short WalkableLongitudinalInputRaw = -150;
        internal const short WalkableTraverseInputRaw = 50;
        internal const short SteepUphillInputRaw = -150;
        internal const short SteepTraverseInputRaw = 50;
        internal const short EdgeInputRaw = -200;
        internal const int ImpulseXMillimetersPerSecond = -6000;
        internal const int ImpulseZMillimetersPerSecond = 2000;

        private const int TickRate = 60;
        private static readonly RuntimeTraceIdentity ControlTraceId = new RuntimeTraceIdentity(1200102);
        private static readonly RuntimeTraceIdentity DashTraceId = new RuntimeTraceIdentity(1200103);
        private const int ImpulseStableEventId = 1200112;
        private const int RootMotionStableEventId = 1200113;

        private readonly Dictionary<int, SlopeSpec> _slopes = new Dictionary<int, SlopeSpec>();
        private CharacterTestCharacterPhysicsRuntime _physics;
        private CharacterTestSlopeNoAllocCase _preparedNoAllocCase;
        private int _preparedNoAllocTick;
        private MutableSemanticScalars _preparedNoAllocSemantics;

        private CharacterTestSlopePlayableFixture(
            CharacterTestCharacterPhysicsRuntime physics,
            GameSlice slice,
            CharacterTestCharacterEntity entity)
        {
            _physics = physics;
            Slice = slice;
            Entity = entity;
            AddSlopeSpec(Slope15BodyId);
            AddSlopeSpec(Slope30BodyId);
            AddSlopeSpec(Slope50BodyId);
        }

        internal GameSlice Slice { get; }
        internal CharacterTestCharacterEntity Entity { get; }
        internal CharacterTestCharacterPhysicsRuntime Physics => _physics;

        internal static CharacterTestSlopePlayableFixture Create(
            GameManager manager,
            CharacterTestCharacterControlOwner owner = CharacterTestCharacterControlOwner.LocalPlayer)
        {
            CharacterTestCharacterPhysicsRuntime physics = BuildFormalRuntime(manager);
            var disabledPlanner = new CharacterTestRuntimeAiPlannerProfile(
                "slope.t6.no-planner",
                initialStateId: 0,
                states: Array.Empty<CharacterTestRuntimeAiPlannerStateDefinition>(),
                transitions: Array.Empty<CharacterTestRuntimeAiPlannerTransitionDefinition>(),
                actions: Array.Empty<CharacterTestRuntimeAiPlannerActionDefinition>());
            var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = physics,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                EnableTraceFileWrite = false,
                RuntimeAiPlannerProfile = disabledPlanner,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                MovableNaturalObjectSpawnSpecs = Array.Empty<CharacterTestMovableNaturalObjectSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true
            });
            bool enqueued = slice.CharacterRuntime.EnqueueSpawn(
                new CharacterTestCharacterSpawnRequest(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 10f,
                    z: 0f,
                    owner));
            Assert.IsTrue(enqueued, "Isolated slope character spawn queue rejected.");
            slice.Tick(1d / TickRate);
            Assert.AreEqual(1, slice.CharacterEntities.Count, "Slope fixture must contain exactly one character.");
            return new CharacterTestSlopePlayableFixture(
                physics,
                slice,
                slice.CharacterEntities[0]);
        }

        internal static CharacterTestCharacterPhysicsRuntime BuildFormalRuntime(GameManager manager)
        {
            Assert.IsNotNull(manager);
            CharacterTestCharacterPhysicsRuntime runtime =
                CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                    manager.WorldObjectSceneRoot,
                    manager.WorldObjectSceneCache,
                    manager.PhysicsSettings);
            Assert.IsNotNull(runtime);
            Assert.AreEqual(100000L, runtime.GroundSnapDistance.RawValue);
            Assert.AreEqual(39, runtime.StaticColliderCount);
            var obbs = new List<CombatPhysicsObbCollider>();
            runtime.World.CopyObbCollidersTo(obbs);
            Assert.AreEqual(3, obbs.Count);
            return runtime;
        }

        internal static void AssertFormalManagerRuntime(GameManager manager)
        {
            CharacterTestCharacterPhysicsRuntime official = BuildFormalRuntime(manager);
            Assert.AreEqual(100000L, official.GroundSnapDistance.RawValue);
            Assert.AreEqual(39, official.StaticColliderCount);
            Assert.IsTrue(official.World.TryGetObbCollider(
                new CombatBodyId(Slope15BodyId),
                new CombatColliderId(SlopeColliderId),
                out _));
            Assert.IsTrue(official.World.TryGetObbCollider(
                new CombatBodyId(Slope30BodyId),
                new CombatColliderId(SlopeColliderId),
                out _));
            Assert.IsTrue(official.World.TryGetObbCollider(
                new CombatBodyId(Slope50BodyId),
                new CombatColliderId(SlopeColliderId),
                out _));

            CharacterTestCombatLevelSceneAdapterResult level =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(
                    manager.WorldObjectSceneRoot,
                    manager.WorldObjectSceneCache);
            Assert.IsFalse(level.HasErrors);
            Assert.AreEqual(ExpectedLevelHash, level.ContentHash.Value);
            Assert.AreEqual(34, level.AabbColliderCount);
            Assert.AreEqual(3, level.ObbColliderCount);
        }

        internal static GameManager FindGameManager(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameManager manager = roots[i].GetComponentInChildren<GameManager>(true);
                if (manager != null)
                    return manager;
            }
            Assert.Fail("Formal CharacterTestScene is missing GameManager.");
            return null;
        }

        internal static FixVector3 ExpectedSlopeNormal(int slopeBodyId)
        {
            switch (slopeBodyId)
            {
                case Slope15BodyId:
                    return new FixVector3(
                        Fix64.FromRaw(258819),
                        Fix64.FromRaw(965926),
                        Fix64.Zero);
                case Slope30BodyId:
                    return new FixVector3(
                        Fix64.FromRaw(500000),
                        Fix64.FromRaw(866025),
                        Fix64.Zero);
                case Slope50BodyId:
                    return new FixVector3(
                        Fix64.FromRaw(766045),
                        Fix64.FromRaw(642788),
                        Fix64.Zero);
                default:
                    throw new ArgumentOutOfRangeException(nameof(slopeBodyId));
            }
        }

        internal static void AssertSlopeNormalWithinTolerance(
            FixVector3 expected,
            FixVector3 actual,
            string context)
        {
            Assert.That(
                actual.X.RawValue,
                Is.InRange(
                    expected.X.RawValue - SlopeNormalToleranceRaw,
                    expected.X.RawValue + SlopeNormalToleranceRaw),
                context + " X");
            Assert.That(
                actual.Y.RawValue,
                Is.InRange(
                    expected.Y.RawValue - SlopeNormalToleranceRaw,
                    expected.Y.RawValue + SlopeNormalToleranceRaw),
                context + " Y");
            Assert.That(
                actual.Z.RawValue,
                Is.InRange(
                    expected.Z.RawValue - SlopeNormalToleranceRaw,
                    expected.Z.RawValue + SlopeNormalToleranceRaw),
                context + " Z");
        }

        internal void PlaceOnSlope(
            int slopeBodyId,
            long longitudinalRaw,
            long lateralRaw,
            bool grounded,
            int yawMilliDegrees)
        {
            SlopeSpec spec = GetSlope(slopeBodyId);
            FixVector3 halfExtents = _physics.GetCharacterHalfExtents(Entity);
            Fix64 radius = Fix64.Min(halfExtents.X.Abs(), halfExtents.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(halfExtents.Y.Abs() - radius, Fix64.Zero);
            Fix64 support = radius + segmentHalf * spec.Collider.AxisY.Y.Abs();
            FixVector3 topPoint = spec.WorldCenter
                + spec.Collider.AxisY * spec.Collider.HalfExtents.Y
                + spec.Collider.AxisZ * Fix64.FromRaw(longitudinalRaw)
                + spec.Collider.AxisX * Fix64.FromRaw(lateralRaw);
            FixVector3 characterCenter = topPoint + spec.Collider.AxisY * support;
            Fix64 feetY = characterCenter.Y - _physics.GetCharacterCenterOffsetY(Entity);

            Entity.ClearMotionRequests();
            Entity.SetInput(CharacterTestCharacterInputComponent.None);
            Entity.SetAction(CharacterTestCharacterActionComponent.None);
            Entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(feetY),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.Z),
                yawMilliDegrees));
            Entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: grounded,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: grounded ? CharacterMovementMode.Idle : CharacterMovementMode.Airborne,
                physicsEnabled: true,
                collisionFlags: grounded
                    ? CombatMotionCollisionFlags.Grounded
                    : CombatMotionCollisionFlags.None,
                surfaceMaterial: grounded ? CombatSurfaceMaterial.Rough : CombatSurfaceMaterial.Default);
        }

        internal void SettleWalkable()
        {
            for (int i = 0; i < 4; i++)
                TickControl(0, 0, CharacterTestRuntimeCommandSources.PlayerInput);
            Assert.IsTrue(Entity.IsGrounded, "Walkable slope fixture did not settle grounded.");
        }

        internal CharacterTestSlopeAuthorityRow TickControl(
            short worldMoveXRaw,
            short worldMoveZRaw,
            int sourceId,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool worldMoveOnly = false)
        {
            RuntimeFrame frame = Slice.CurrentFrame;
            CharacterTestControlIntent intent = CharacterTestControlIntent.FromDeterministic(
                Entity.Id.Value,
                worldMoveOnly ? (short)0 : worldMoveXRaw,
                worldMoveOnly ? (short)0 : worldMoveZRaw,
                worldMoveXRaw,
                worldMoveZRaw,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed: false);
            RuntimeCommandValidationResult enqueue = Slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    sourceId,
                    intent,
                    ControlTraceId));
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
            Slice.Tick(1d / TickRate);
            return Capture(frame);
        }

        private CharacterTestSlopeAuthorityRow TickControlNoAssert(
            short worldMoveXRaw,
            short worldMoveZRaw,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli)
        {
            RuntimeFrame frame = Slice.CurrentFrame;
            CharacterTestControlIntent intent = CharacterTestControlIntent.FromDeterministic(
                Entity.Id.Value,
                worldMoveXRaw,
                worldMoveZRaw,
                worldMoveXRaw,
                worldMoveZRaw,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                movementSpeedScaleMilli: movementSpeedScaleMilli);
            RuntimeCommandValidationResult enqueue = Slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    intent,
                    ControlTraceId));
            _preparedNoAllocSemantics.RecordCommandAccepted(enqueue.Success);
            Slice.Tick(1d / TickRate);
            return Capture(frame);
        }

        internal CharacterTestSlopeAuthorityRow TickWithoutCommand()
        {
            RuntimeFrame frame = Slice.CurrentFrame;
            Slice.Tick(1d / TickRate);
            return Capture(frame);
        }

        internal bool AssignDashStrikeSlot()
        {
            return Slice.AbilityHotbar.AssignSlot(
                DashStrikeSlot,
                DashStrikeAbilityId,
                out _);
        }

        internal CharacterTestSlopeAuthorityRow[] RunDashStrike()
        {
            RuntimeFrame frame = Slice.CurrentFrame;
            RuntimeCommandValidationResult cast = Slice.EnqueueCastAbilitySlot(
                Entity.Id.Value,
                DashStrikeSlot,
                DashStrikeAbilityId,
                CharacterTestRuntimeCommandSources.PlayerInput,
                DashTraceId);
            Assert.IsTrue(cast.Success, cast.Error.Message);
            var rows = new CharacterTestSlopeAuthorityRow[DashTicks];
            Slice.Tick(1d / TickRate);
            rows[0] = Capture(frame);
            for (int i = 1; i < rows.Length; i++)
                rows[i] = TickWithoutCommand();
            return rows;
        }

        internal void EnqueueFormalImpulse()
        {
            Entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                ImpulseXMillimetersPerSecond,
                0,
                ImpulseZMillimetersPerSecond,
                CharacterMovementMode.Run,
                new CharacterActionId(990327),
                ImpulseStableEventId,
                CharacterTestCharacterMotionRequestSpace.World));
        }

        internal long GetSlopeLongitudinalRaw(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(slopeBodyId);
            FixVector3 center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.YMillimeters)
                    + _physics.GetCharacterCenterOffsetY(Entity),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            return (center - slope.WorldCenter).Dot(slope.Collider.AxisZ).RawValue;
        }

        internal long GetSlopeLongitudinalHalfExtentRaw(int slopeBodyId)
        {
            return GetSlope(slopeBodyId).Collider.HalfExtents.Z.RawValue;
        }

        internal bool TryResolveAdjacentSeamAabb(
            int slopeBodyId,
            int edgeSign,
            out CombatBodyId bodyId,
            out CombatColliderId colliderId,
            out int surfaceYMillimeters)
        {
            const long probeBeyondEdgeRaw = 100000L;
            const long surfaceHeightToleranceRaw = 5000L;
            SlopeSpec slope = GetSlope(slopeBodyId);
            FixVector3 topEdge = slope.WorldCenter
                + slope.Collider.AxisY * slope.Collider.HalfExtents.Y
                + slope.Collider.AxisZ * (slope.Collider.HalfExtents.Z * Fix64.FromInt(edgeSign));
            FixVector3 beyond = topEdge
                + slope.Collider.AxisZ * Fix64.FromRaw(edgeSign * probeBeyondEdgeRaw);
            var aabbs = new List<CombatPhysicsAabbCollider>();
            _physics.World.CopyAabbCollidersTo(aabbs);

            long bestHeightDeltaRaw = long.MaxValue;
            bodyId = default;
            colliderId = default;
            Fix64 bestSurfaceY = Fix64.Zero;
            for (int i = 0; i < aabbs.Count; i++)
            {
                CombatPhysicsAabbCollider candidate = aabbs[i];
                if (!_physics.World.TryGetBody(candidate.BodyId, out CombatPhysicsBody body))
                    continue;
                FixVector3 min = candidate.GetWorldMin(body.Position);
                FixVector3 max = candidate.GetWorldMax(body.Position);
                if (beyond.X < min.X || beyond.X > max.X || beyond.Z < min.Z || beyond.Z > max.Z)
                    continue;

                long heightDeltaRaw = Math.Abs(max.Y.RawValue - topEdge.Y.RawValue);
                if (heightDeltaRaw > surfaceHeightToleranceRaw || heightDeltaRaw >= bestHeightDeltaRaw)
                    continue;

                bestHeightDeltaRaw = heightDeltaRaw;
                bodyId = candidate.BodyId;
                colliderId = candidate.ColliderId;
                bestSurfaceY = max.Y;
            }

            if (bestHeightDeltaRaw == long.MaxValue)
            {
                surfaceYMillimeters = 0;
                return false;
            }

            surfaceYMillimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(bestSurfaceY);
            return true;
        }

        internal int ComputeHorizontalEdgeGapMillimeters(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(slopeBodyId);
            return ComputeHorizontalEdgeGapMillimeters(slope, row);
        }

        internal int ComputeLongitudinalEdgeGapMillimeters(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(slopeBodyId);
            long longitudinalRaw = Math.Abs(GetSlopeLongitudinalRaw(slopeBodyId, row));
            FixVector3 characterHalf = _physics.GetCharacterHalfExtents(Entity);
            Fix64 radius = Fix64.Min(characterHalf.X.Abs(), characterHalf.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(characterHalf.Y.Abs() - radius, Fix64.Zero);
            Fix64 support = radius + segmentHalf * slope.Collider.AxisZ.Y.Abs();
            long gapRaw = longitudinalRaw
                - slope.Collider.HalfExtents.Z.RawValue
                - support.RawValue;
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                gapRaw > 0L ? Fix64.FromRaw(gapRaw) : Fix64.Zero);
        }

        private int ComputeHorizontalEdgeGapMillimetersNoAssert(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            return ComputeHorizontalEdgeGapMillimeters(_slopes[slopeBodyId], row);
        }

        private int ComputeHorizontalEdgeGapMillimeters(
            SlopeSpec slope,
            CharacterTestSlopeAuthorityRow row)
        {
            FixVector3 point = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                Fix64.Zero,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            FixVector3 center = new FixVector3(slope.WorldCenter.X, Fix64.Zero, slope.WorldCenter.Z);
            FixVector3 axis = new FixVector3(slope.Collider.AxisX.X, Fix64.Zero, slope.Collider.AxisX.Z);
            Fix64 lateral = (point - center).Dot(axis).Abs();
            FixVector3 characterHalf = _physics.GetCharacterHalfExtents(Entity);
            Fix64 radius = Fix64.Min(characterHalf.X.Abs(), characterHalf.Z.Abs());
            Fix64 gap = lateral - slope.Collider.HalfExtents.X - radius;
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                gap > Fix64.Zero ? gap : Fix64.Zero);
        }

        internal bool IsWithinSlopeInterior(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(slopeBodyId);
            return IsWithinSlopeInterior(slope, row);
        }

        private bool IsWithinSlopeInteriorNoAssert(
            int slopeBodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            return IsWithinSlopeInterior(_slopes[slopeBodyId], row);
        }

        private bool IsWithinSlopeInterior(
            SlopeSpec slope,
            CharacterTestSlopeAuthorityRow row)
        {
            FixVector3 center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.YMillimeters)
                    + _physics.GetCharacterCenterOffsetY(Entity),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            FixVector3 local = center - slope.WorldCenter;
            Fix64 longitudinal = local.Dot(slope.Collider.AxisZ).Abs();
            Fix64 lateral = local.Dot(slope.Collider.AxisX).Abs();
            FixVector3 characterHalf = _physics.GetCharacterHalfExtents(Entity);
            Fix64 radius = Fix64.Min(characterHalf.X.Abs(), characterHalf.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(characterHalf.Y.Abs() - radius, Fix64.Zero);
            Fix64 longitudinalSupport = radius
                + segmentHalf * slope.Collider.AxisZ.Y.Abs();
            Fix64 lateralSupport = radius
                + segmentHalf * slope.Collider.AxisX.Y.Abs();
            Fix64 longitudinalLimit = Fix64.Max(
                Fix64.Zero,
                slope.Collider.HalfExtents.Z - longitudinalSupport);
            Fix64 lateralLimit = Fix64.Max(
                Fix64.Zero,
                slope.Collider.HalfExtents.X - lateralSupport);
            return longitudinal <= longitudinalLimit && lateral <= lateralLimit;
        }

        internal CharacterTestSlopeAuthorityRow[] RunDeterminismSequence()
        {
            ResetForDeterminismSequence();
            return RunDeterminismSequenceAlreadyPlaced();
        }

        internal void PlaceForDeterminismFirstTick()
        {
            PlaceOnSlope(
                Slope30BodyId,
                longitudinalRaw: 1500000,
                lateralRaw: 0,
                grounded: true,
                UphillYawMilliDegrees);
        }

        internal CharacterTestSlopeAuthorityRow TickDeterminismFirstTick()
        {
            return TickControl(
                -150,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput);
        }

        internal void ResetForDeterminismSequence()
        {
            PlaceOnSlope(
                Slope30BodyId,
                longitudinalRaw: 1500000,
                lateralRaw: 0,
                grounded: true,
                UphillYawMilliDegrees);
            SettleWalkable();
        }

        internal CharacterTestSlopeAuthorityRow[] RunDeterminismSequenceAlreadyPlaced()
        {
            const int count = 90;
            var rows = new CharacterTestSlopeAuthorityRow[count];
            for (int i = 0; i < rows.Length; i++)
            {
                short input = i < 30 ? (short)-150 : i < 60 ? (short)0 : (short)150;
                rows[i] = TickControl(
                    input,
                    0,
                    CharacterTestRuntimeCommandSources.PlayerInput);
            }
            return rows;
        }

        internal void ReplaceMotionPhysicsRuntime(CharacterTestCharacterPhysicsRuntime replacement)
        {
            Assert.IsNotNull(replacement);
            FieldInfo field = typeof(CharacterTestCharacterRuntimeModule).GetField(
                "_motionSystem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var motion = field.GetValue(Slice.CharacterRuntime) as CharacterTestCharacterMotionSystem;
            Assert.IsNotNull(motion);
            motion.SetPhysicsRuntime(replacement);
            _physics = replacement;
            _slopes.Clear();
            AddSlopeSpec(Slope15BodyId);
            AddSlopeSpec(Slope30BodyId);
            AddSlopeSpec(Slope50BodyId);
        }

        internal void PrepareNoAllocCase(CharacterTestSlopeNoAllocCase item)
        {
            _preparedNoAllocCase = item;
            _preparedNoAllocTick = 0;
            PlacePreparedNoAllocCase(item);
            _preparedNoAllocSemantics = new MutableSemanticScalars(Entity);
        }

        internal void BeginNoAllocSampleWindow()
        {
            _preparedNoAllocTick = 0;
            PlacePreparedNoAllocCase(_preparedNoAllocCase);
            _preparedNoAllocSemantics.Reset(Entity);
        }

        private void PlacePreparedNoAllocCase(CharacterTestSlopeNoAllocCase item)
        {
            switch (item)
            {
                case CharacterTestSlopeNoAllocCase.ThirtyUphill:
                    PlaceOnSlope(Slope30BodyId, 1800000, 0, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyDownhillSnap:
                    PlaceOnSlope(Slope30BodyId, -1800000, 0, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyTraverse:
                    PlaceOnSlope(Slope30BodyId, 0, 250000, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyIdle:
                    PlaceOnSlope(Slope30BodyId, 0, 0, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                case CharacterTestSlopeNoAllocCase.FiftyBlocked:
                    PlaceOnSlope(Slope50BodyId, 500000, 0, false, UphillYawMilliDegrees);
                    break;
                case CharacterTestSlopeNoAllocCase.EdgeSupportMiss:
                    PlaceOnSlope(Slope30BodyId, 0, 1900000, false, UphillYawMilliDegrees);
                    break;
                case CharacterTestSlopeNoAllocCase.RootMotionStaticOnly:
                    PlaceOnSlope(Slope30BodyId, 1000000, 0, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                case CharacterTestSlopeNoAllocCase.ImpulseRaw:
                    PlaceOnSlope(Slope30BodyId, 1000000, 0, true, UphillYawMilliDegrees);
                    SettleWalkable();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item));
            }
        }

        internal CharacterTestSlopeAuthorityRow TickPreparedNoAllocCase()
        {
            CharacterTestSlopeAuthorityRow row;
            switch (_preparedNoAllocCase)
            {
                case CharacterTestSlopeNoAllocCase.ThirtyUphill:
                    row = TickControlNoAssert(-50, 0, movementSpeedScaleMilli: 100);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyDownhillSnap:
                    row = TickControlNoAssert(50, 0, movementSpeedScaleMilli: 100);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyTraverse:
                    row = TickControlNoAssert(0, 20, movementSpeedScaleMilli: 100);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyIdle:
                    row = TickControlNoAssert(0, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.FiftyBlocked:
                    row = TickControlNoAssert(-50, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.EdgeSupportMiss:
                    row = TickControlNoAssert(0, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.RootMotionStaticOnly:
                    Entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDeltaRaw(
                        -1,
                        0,
                        0,
                        CharacterTestCharacterMotionRequestSpace.World,
                        CharacterTestCharacterActionConfigSource.DashStrikeActionId,
                        RootMotionStableEventId,
                        CharacterMotionObstaclePolicy.StaticOnly));
                    row = TickWithoutCommand();
                    break;
                case CharacterTestSlopeNoAllocCase.ImpulseRaw:
                    Entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                        (_preparedNoAllocTick & 1) == 0
                            ? ImpulseXMillimetersPerSecond
                            : -ImpulseXMillimetersPerSecond,
                        0,
                        0,
                        CharacterMovementMode.Run,
                        new CharacterActionId(990327),
                        ImpulseStableEventId,
                        CharacterTestCharacterMotionRequestSpace.World));
                    row = TickWithoutCommand();
                    break;
                default:
                    throw new InvalidOperationException("No NoAlloc slope case has been prepared.");
            }
            _preparedNoAllocTick++;
            int expectedSlopeBodyId = ResolvePreparedNoAllocSlopeBodyId();
            bool edgeBeyondGuard = _preparedNoAllocCase == CharacterTestSlopeNoAllocCase.EdgeSupportMiss
                && ComputeHorizontalEdgeGapMillimetersNoAssert(Slope30BodyId, row) > EdgeGuardMillimeters;
            bool withinExpectedSlopeInterior = IsWithinSlopeInteriorNoAssert(
                expectedSlopeBodyId,
                row);
            _preparedNoAllocSemantics.Observe(
                row,
                expectedSlopeBodyId,
                edgeBeyondGuard,
                withinExpectedSlopeInterior);
            return row;
        }

        private int ResolvePreparedNoAllocSlopeBodyId()
        {
            return _preparedNoAllocCase == CharacterTestSlopeNoAllocCase.FiftyBlocked
                ? Slope50BodyId
                : Slope30BodyId;
        }

        internal CharacterTestSlopeSemanticScalars GetPreparedNoAllocSemantics()
        {
            return _preparedNoAllocSemantics.CreateSnapshot();
        }

        public void Dispose()
        {
            Slice?.Dispose();
        }

        private CharacterTestSlopeAuthorityRow Capture(RuntimeFrame frame)
        {
            bool hasDiagnostic = Slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                Entity.Id.Value,
                out CharacterTestCharacterMotionDiagnostic diagnostic);
            return new CharacterTestSlopeAuthorityRow(frame, Entity, hasDiagnostic, diagnostic);
        }

        private void AddSlopeSpec(int bodyIdValue)
        {
            var bodyId = new CombatBodyId(bodyIdValue);
            Assert.IsTrue(_physics.World.TryGetBody(bodyId, out CombatPhysicsBody body));
            Assert.IsTrue(_physics.World.TryGetObbCollider(
                bodyId,
                new CombatColliderId(SlopeColliderId),
                out CombatPhysicsObbCollider collider));
            _slopes.Add(bodyIdValue, new SlopeSpec(body, collider));
        }

        private SlopeSpec GetSlope(int bodyId)
        {
            Assert.IsTrue(_slopes.TryGetValue(bodyId, out SlopeSpec slope));
            return slope;
        }

        private readonly struct SlopeSpec
        {
            public SlopeSpec(CombatPhysicsBody body, CombatPhysicsObbCollider collider)
            {
                Body = body;
                Collider = collider;
                WorldCenter = collider.GetWorldCenter(body.Position);
            }

            public CombatPhysicsBody Body { get; }
            public CombatPhysicsObbCollider Collider { get; }
            public FixVector3 WorldCenter { get; }
        }

        private sealed class MutableSemanticScalars
        {
            private int _ticks;
            private int _grounded;
            private int _slopeContacts;
            private int _expectedSlopeContacts;
            private int _expectedSlopeInterior;
            private int _steep;
            private int _snap;
            private int _edgeBeyondGuard;
            private int _edgeAirborne;
            private int _edgeSlopeSnap;
            private int _nonZeroRequestedX;
            private int _nonZeroAppliedOrResolvedX;
            private int _startX;
            private int _startY;
            private int _startZ;
            private int _finalX;
            private int _finalY;
            private int _finalZ;
            private int _maximumY;
            private bool _sawAirborne;
            private bool _commandsAccepted = true;

            public MutableSemanticScalars(CharacterTestCharacterEntity entity)
            {
                Reset(entity);
            }

            public void Reset(CharacterTestCharacterEntity entity)
            {
                _ticks = 0;
                _grounded = 0;
                _slopeContacts = 0;
                _expectedSlopeContacts = 0;
                _expectedSlopeInterior = 0;
                _steep = 0;
                _snap = 0;
                _edgeBeyondGuard = 0;
                _edgeAirborne = 0;
                _edgeSlopeSnap = 0;
                _nonZeroRequestedX = 0;
                _nonZeroAppliedOrResolvedX = 0;
                _startX = entity.Transform.XMillimeters;
                _startY = entity.Transform.YMillimeters;
                _startZ = entity.Transform.ZMillimeters;
                _finalX = _startX;
                _finalY = _startY;
                _finalZ = _startZ;
                _maximumY = _startY;
                _sawAirborne = false;
                _commandsAccepted = true;
            }

            public void Observe(
                CharacterTestSlopeAuthorityRow row,
                int expectedSlopeBodyId,
                bool edgeBeyondGuard,
                bool withinExpectedSlopeInterior)
            {
                _ticks++;
                _grounded += row.Grounded ? 1 : 0;
                _slopeContacts += row.HasDiagnostic
                    && row.Diagnostic.TargetShapeKind == CombatPhysicsShapeKind.Obb ? 1 : 0;
                _expectedSlopeContacts += row.IsSlopeContact(expectedSlopeBodyId) ? 1 : 0;
                _expectedSlopeInterior += withinExpectedSlopeInterior ? 1 : 0;
                _steep += row.IsSlopeContact(expectedSlopeBodyId)
                    && row.Diagnostic.SurfaceClassification == CharacterTestMotionSurfaceClassification.Steep ? 1 : 0;
                _snap += row.IsSlopeContact(expectedSlopeBodyId)
                    && (row.Diagnostic.CollisionFlags & CombatMotionCollisionFlags.GroundSnap) != 0 ? 1 : 0;
                if (edgeBeyondGuard)
                {
                    _edgeBeyondGuard++;
                    _edgeAirborne += row.Grounded ? 0 : 1;
                    _edgeSlopeSnap += row.IsSlopeContact(Slope30BodyId)
                        && (row.Diagnostic.CollisionFlags & CombatMotionCollisionFlags.GroundSnap) != 0
                            ? 1
                            : 0;
                }
                if (row.HasDiagnostic)
                {
                    _nonZeroRequestedX += row.Diagnostic.RequestedDelta.X != Fix64.Zero ? 1 : 0;
                    _nonZeroAppliedOrResolvedX += row.Diagnostic.AppliedDelta.X != Fix64.Zero
                        || row.Diagnostic.ResolvedVelocity.X != Fix64.Zero
                            ? 1
                            : 0;
                }
                _finalX = row.XMillimeters;
                _finalY = row.YMillimeters;
                _finalZ = row.ZMillimeters;
                _maximumY = Math.Max(_maximumY, row.YMillimeters);
                _sawAirborne |= !row.Grounded;
            }

            public void RecordCommandAccepted(bool accepted)
            {
                _commandsAccepted &= accepted;
            }

            public CharacterTestSlopeSemanticScalars CreateSnapshot()
            {
                return new CharacterTestSlopeSemanticScalars(
                    _ticks,
                    _grounded,
                    _slopeContacts,
                    _expectedSlopeContacts,
                    _expectedSlopeInterior,
                    _steep,
                    _snap,
                    _edgeBeyondGuard,
                    _edgeAirborne,
                    _edgeSlopeSnap,
                    _nonZeroRequestedX,
                    _nonZeroAppliedOrResolvedX,
                    _startX,
                    _startY,
                    _startZ,
                    _finalX,
                    _finalY,
                    _finalZ,
                    _maximumY,
                    _sawAirborne,
                    _commandsAccepted);
            }
        }
    }
}
