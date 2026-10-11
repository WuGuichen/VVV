using System;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestAuthorityPoseTerrainTests
    {
        internal const string ClipKey = "clip.terrain.test";
        internal const int FirstRootChannelId = 1;
        internal const int FirstMidChannelId = 2;
        internal const int FirstTipChannelId = 3;
        private const int SecondMidChannelId = 5;
        private const int GroundLayer = 0;
        private const int TailPartStateId = CharacterTestAuthoredContentIds.D400_Id43;

        [Test]
        public void Blend1DBaseSource_UsesActualAnimationSpeed_WhenMovementModeRemainsRun()
        {
            TerrainFixture fixture = CreateFixture(limbCount: 1);
            fixture.Entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: true,
                movementMode: CharacterMovementMode.Run);
            fixture.Entity.SetAnimation(CharacterTestCharacterAnimationComponent.IdleGrounded);
            var blend = new MxAnimationBlend1DDefinition(
                "blend.speed",
                "speed",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend1DPoint(0, new ResourceKey("idle", ResourceTypeIds.AnimationClip)),
                    new MxAnimationBlend1DPoint(500, new ResourceKey("walk", ResourceTypeIds.AnimationClip)),
                    new MxAnimationBlend1DPoint(1000, new ResourceKey("run", ResourceTypeIds.AnimationClip))
                });

            int sourceIndex = CharacterTestAuthorityPoseTerrainSystem.ResolveBlend1DSourceIndex(
                fixture.Entity,
                blend);

            Assert.AreEqual(0, sourceIndex, "A run-capable movement mode must not force Run while actual velocity is zero.");
        }

        [Test]
        public void ResolvedFrame_SingleYawProducesRootLocalPositionAndFrozenOrientation()
        {
            TerrainFixture fixture = CreateFixture(
                limbCount: 1,
                rootYMillimeters: 1000,
                tipXOffsetMillimeters: 1000,
                rootTransform: new CharacterTestCharacterTransformComponent(10f, 0f, 20f, 90f));
            CombatPhysicsWorld physics = CreateGroundWorld();

            Assert.IsTrue(fixture.System.TickForTests(
                fixture.Entity,
                physics,
                new RuntimeFrame(7),
                ClipKey,
                localFrame: 0,
                loop: true));

            CharacterTestTerrainIkLimbState state = fixture.Entity.TerrainIkState.GetLimb(0);
            AssertVectorNear(
                state.LastValidTargetWorld,
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.FromInt(19)));
            CharacterTestResolvedAuthorityPoseFrame frame = fixture.Entity.ResolvedAuthorityPoseFrame;
            Assert.AreEqual(7L, frame.RuntimeFrame);
            Assert.AreEqual(3, frame.Count);
            Assert.AreEqual(fixture.Definition.SourceRevision, frame.DefinitionRevision);
            Assert.AreEqual(fixture.BaseSourceRevision, frame.BaseSourceRevision);
            Assert.IsTrue(frame.TryGetSample(FirstRootChannelId, out CharacterTestResolvedAuthorityPoseSample root));
            Assert.IsTrue(frame.TryGetSample(FirstMidChannelId, out CharacterTestResolvedAuthorityPoseSample mid));
            Assert.IsTrue(frame.TryGetSample(FirstTipChannelId, out CharacterTestResolvedAuthorityPoseSample tip));
            AssertVectorNear(root.Position, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            AssertVectorNear(tip.Position, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
            AssertForwardAlongChild(root.Basis.Forward, mid.Position - root.Position);
            AssertForwardAlongChild(mid.Basis.Forward, tip.Position - mid.Position);
            Assert.That(Abs(root.Basis.Up.Y.RawValue), Is.LessThan(Fix64.One.RawValue),
                "root/mid plane basis must not reuse the ground normal");
            AssertVectorNear(tip.Basis.Up, AxisY());
            AssertVectorNear(tip.Basis.Forward, AxisZ());
            Assert.AreEqual(
                CharacterTestAuthorityPoseSampleSourceMask.Base
                    | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                tip.SourceMask);
            Assert.AreEqual(CharacterTestAuthorityPoseFailure.Success, tip.Failure);
        }

        [Test]
        public void LossRecovery_WorldOriginTargetRemainsValidAndHistoryIsBounded()
        {
            TerrainFixture fixture = CreateFixture(
                limbCount: 1,
                enterFrames: 2,
                exitFrames: 2,
                lostFrames: 1);
            CombatPhysicsWorld ground = CreateGroundWorld();
            var empty = new CombatPhysicsWorld();

            Tick(fixture, ground, 1);
            CharacterTestTerrainIkLimbState state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(0L, state.LastValidTargetWorld.X.RawValue);
            Assert.AreEqual(0L, state.LastValidTargetWorld.Y.RawValue);
            Assert.AreEqual(0L, state.LastValidTargetWorld.Z.RawValue);
            Assert.IsTrue(state.HasRetainedTarget, "world origin is a legal retained ground target");
            Assert.AreEqual(Fix64.Half.RawValue, state.CurrentWeight.RawValue);

            Tick(fixture, ground, 2);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(Fix64.One.RawValue, state.CurrentWeight.RawValue);

            Tick(fixture, empty, 3);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(1, state.ConsecutiveMissCount);
            Assert.AreEqual(Fix64.One.RawValue, state.CurrentWeight.RawValue);
            Assert.IsTrue(state.HasRetainedTarget);
            Assert.AreEqual(CharacterTestAuthorityPoseFailure.NoHit, state.CurrentFailure);

            Tick(fixture, empty, 4);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(Fix64.Half.RawValue, state.CurrentWeight.RawValue);
            Assert.IsTrue(state.HasRetainedTarget);

            Tick(fixture, empty, 5);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(Fix64.Zero.RawValue, state.CurrentWeight.RawValue);
            Assert.AreEqual(-1L, state.LastValidFrame);
            Assert.IsFalse(state.HasRetainedTarget);

            for (int frame = 6; frame < 30; frame++)
                Tick(fixture, empty, frame);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(state.MissCountCap, state.ConsecutiveMissCount,
                "miss history must saturate at the authored hysteresis bound");

            Tick(fixture, ground, 30);
            state = fixture.Entity.TerrainIkState.GetLimb(0);
            Assert.AreEqual(Fix64.Half.RawValue, state.CurrentWeight.RawValue);
            Assert.IsTrue(state.HasRetainedTarget);
            Tick(fixture, ground, 31);
            Assert.AreEqual(
                Fix64.One.RawValue,
                fixture.Entity.TerrainIkState.GetLimb(0).CurrentWeight.RawValue);
        }

        [Test]
        public void ReachMirrorAndDegeneratePole_AreTypedAndDeterministic()
        {
            TerrainFixture unreachable = CreateFixture(
                limbCount: 1,
                rootYMillimeters: 3000);
            Tick(unreachable, CreateGroundWorld(), 1);
            Assert.IsTrue(unreachable.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                FirstTipChannelId,
                out CharacterTestResolvedAuthorityPoseSample unreachableTip));
            Assert.AreEqual(CharacterTestAuthorityPoseFailure.OutOfReach, unreachableTip.Failure);
            Assert.AreEqual(CharacterTestAuthorityPoseSampleSourceMask.Base, unreachableTip.SourceMask);

            TerrainFixture mirrored = CreateFixture(limbCount: 2, mirrorPoles: true);
            Tick(mirrored, CreateGroundWorld(), 2);
            Assert.IsTrue(mirrored.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                FirstMidChannelId,
                out CharacterTestResolvedAuthorityPoseSample leftMid));
            Assert.IsTrue(mirrored.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                SecondMidChannelId,
                out CharacterTestResolvedAuthorityPoseSample rightMid));
            AssertRawNear(leftMid.Position.X.RawValue, -rightMid.Position.X.RawValue);
            AssertRawNear(leftMid.Position.Y.RawValue, rightMid.Position.Y.RawValue);
            AssertRawNear(leftMid.Position.Z.RawValue, rightMid.Position.Z.RawValue);
            AssertRawNear(leftMid.Basis.Forward.X.RawValue, -rightMid.Basis.Forward.X.RawValue);
            AssertRawNear(leftMid.Basis.Forward.Y.RawValue, rightMid.Basis.Forward.Y.RawValue);

            TerrainFixture degenerate = CreateFixture(
                limbCount: 1,
                poleOverride: new CharacterAuthorityPoseQuantizedVector3(0, -32767, 0));
            Tick(degenerate, CreateGroundWorld(), 3);
            Assert.IsTrue(degenerate.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                FirstMidChannelId,
                out CharacterTestResolvedAuthorityPoseSample fallbackMid));
            Assert.Greater(fallbackMid.Position.X.RawValue, 0L,
                "parallel authored pole must use deterministic ModelRoot +X projection fallback");
            Assert.AreEqual(CharacterTestAuthorityPoseFailure.Success, fallbackMid.Failure);
        }

        [Test]
        public void EligibilityAndSourceIdentity_ExposeFrozenTypedFailures()
        {
            AssertFailure(
                CreateFixture(1, allowedMotionIds: new[] { "Run" }),
                CharacterTestAuthorityPoseFailure.MotionDisabled);
            AssertFailure(
                CreateFixture(1, allowedPostureIds: new[] { 1201601 }),
                CharacterTestAuthorityPoseFailure.PostureDisabled);

            TerrainFixture flying = CreateFixture(1);
            flying.Entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: false,
                movementMode: CharacterMovementMode.Airborne);
            AssertFailure(flying, CharacterTestAuthorityPoseFailure.Flying);

            TerrainFixture dead = CreateFixture(1);
            dead.Entity.BeginDeath(frame: 1, sourceEntityId: 9, traceId: 10);
            AssertFailure(dead, CharacterTestAuthorityPoseFailure.Dead);

            TerrainFixture definitionStale = CreateFixture(1);
            CharacterAuthorityPoseRigDefinition otherDefinition = CreateDefinition(
                CreateRig(1, definitionRevision: 2));
            Assert.IsTrue(definitionStale.Entity.TerrainIkState.Configure(
                otherDefinition,
                definitionStale.BaseSourceRevision,
                definitionStale.BaseSourceHash));
            AssertFailure(definitionStale, CharacterTestAuthorityPoseFailure.DefinitionStale);

            TerrainFixture sourceStale = CreateFixture(1);
            Assert.IsTrue(sourceStale.Entity.TerrainIkState.Configure(
                sourceStale.Definition,
                baseSourceRevision: 99,
                baseSourceContentHash: "sha256:2222222222222222222222222222222222222222222222222222222222222222"));
            AssertFailure(sourceStale, CharacterTestAuthorityPoseFailure.SourceStale);
        }

        [Test]
        public void ActionAndSeveredEligibility_ReadRealDeterministicAuthoritySources()
        {
            var runtime = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            try
            {
                CharacterActionConfig taggedAction = FindTaggedAction(runtime.ActionRuntime.Registry);
                TerrainFixture actionFixture = ConfigureFixture(
                    runtime.World,
                    runtime.ActionRuntime,
                    CreateRig(1, disabledActionTags: new[] { taggedAction.Tags[0] }));
                actionFixture.Entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                    activeActionId: taggedAction.ActionId,
                    state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                    phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                    localFrame: 0,
                    lastRejectReason: default,
                    lastAnimationActionKey: string.Empty,
                    animationActionKeyThisFrame: string.Empty,
                    animationTransitionFrames: 0,
                    animationHoldFrames: 0,
                    jumpImpulseYMillimetersPerSecond: 0));
                AssertFailure(actionFixture, CharacterTestAuthorityPoseFailure.ActionDisabled);
            }
            finally
            {
                runtime.Dispose();
            }

            CharacterTestCharacterDefinition dragon = FindDragonDefinition();
            var dragonWorld = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity dragonEntity = dragonWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    dragon.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                dragon);
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackageForEntity(
                dragonEntity);
            Assert.NotNull(package);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                dragonEntity.ReinitializeBodyPartState(package.Geometry));
            TerrainFixture severedFixture = ConfigureFixture(
                dragonWorld,
                actionRuntime: null,
                CreateRig(1, partStateId: TailPartStateId),
                existingEntity: dragonEntity);
            Tick(severedFixture, CreateGroundWorld(), 1);
            SeverPart(dragonEntity, TailPartStateId, breakFrame: 2, severFrame: 3);
            AssertFailure(severedFixture, CharacterTestAuthorityPoseFailure.Severed, frame: 4);
        }

        [Test]
        public void FourLimbResolvedOrder_WarmedTickAllocatesZeroBytes()
        {
            TerrainFixture fixture = CreateFixture(limbCount: 4);
            CombatPhysicsWorld physics = CreateGroundWorld();
            for (int frame = 1; frame <= 16; frame++)
                Tick(fixture, physics, frame);

            CharacterTestResolvedAuthorityPoseFrame resolved = fixture.Entity.ResolvedAuthorityPoseFrame;
            Assert.AreEqual(12, resolved.Count);
            for (int i = 1; i < resolved.Count; i++)
            {
                Assert.Less(
                    resolved[i - 1].ChannelId.CompareTo(resolved[i].ChannelId),
                    0,
                    "resolved samples must stay in canonical channel-id order");
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 17; frame <= 80; frame++)
                fixture.System.TickForTests(fixture.Entity, physics, new RuntimeFrame(frame), ClipKey, 0, true);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "warmed four-limb terrain IK Tick must allocate 0 B");
        }

        private static TerrainFixture CreateFixture(
            int limbCount,
            int rootYMillimeters = 1000,
            int tipXOffsetMillimeters = 0,
            int enterFrames = 1,
            int exitFrames = 1,
            int lostFrames = 0,
            bool mirrorPoles = false,
            CharacterAuthorityPoseQuantizedVector3? poleOverride = null,
            string[] allowedMotionIds = null,
            int[] allowedPostureIds = null,
            string[] disabledActionTags = null,
            int partStateId = 0,
            CharacterTestCharacterTransformComponent rootTransform = default)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterAuthorityPoseRigRuntimeBinding rig = CreateRig(
                limbCount,
                rootYMillimeters,
                tipXOffsetMillimeters,
                enterFrames,
                exitFrames,
                lostFrames,
                mirrorPoles,
                poleOverride,
                allowedMotionIds,
                allowedPostureIds,
                disabledActionTags,
                partStateId);
            return ConfigureFixture(
                world,
                actionRuntime: null,
                rig,
                rootTransform: rootTransform,
                tipXOffsetMillimeters: tipXOffsetMillimeters);
        }

        internal static TerrainFixture ConfigureFixture(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterTestCharacterEntity existingEntity = null,
            CharacterTestCharacterTransformComponent rootTransform = default,
            int tipXOffsetMillimeters = 0)
        {
            CharacterTestCharacterEntity entity = existingEntity ?? world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: 9101,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                CreateCharacterDefinition());
            if (rootTransform.XMillimeters != 0
                || rootTransform.YMillimeters != 0
                || rootTransform.ZMillimeters != 0
                || rootTransform.YawMilliDegrees != 0)
            {
                entity.SetTransform(rootTransform);
            }
            entity.SetMotionState(0f, Vector2.zero, isGrounded: true, movementMode: CharacterMovementMode.Idle);
            CharacterAuthorityPoseRigDefinition definition = CreateDefinition(rig);
            CharacterSocketBakeCatalog catalog = CreateCatalog(rig, tipXOffsetMillimeters);
            var system = new CharacterTestAuthorityPoseTerrainSystem(actionRuntime);
            const int baseSourceRevision = 7;
            const string baseSourceHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
            Assert.IsTrue(system.ConfigureForTests(
                entity,
                definition,
                catalog,
                baseSourceRevision,
                baseSourceHash));
            return new TerrainFixture(
                world,
                entity,
                system,
                definition,
                catalog,
                baseSourceRevision,
                baseSourceHash);
        }

        internal static CharacterAuthorityPoseRigRuntimeBinding CreateRig(
            int limbCount,
            int rootYMillimeters = 1000,
            int tipXOffsetMillimeters = 0,
            int enterFrames = 1,
            int exitFrames = 1,
            int lostFrames = 0,
            bool mirrorPoles = false,
            CharacterAuthorityPoseQuantizedVector3? poleOverride = null,
            string[] allowedMotionIds = null,
            int[] allowedPostureIds = null,
            string[] disabledActionTags = null,
            int partStateId = 0,
            int definitionRevision = 1)
        {
            var channels = new CharacterAuthorityPoseChannelRuntimeBinding[limbCount * 3];
            var limbs = new CharacterAuthorityPoseLimbRuntimeBinding[limbCount];
            for (int i = 0; i < limbCount; i++)
            {
                string prefix = "channel." + i + ".";
                int channelStart = i * 3;
                channels[channelStart] = new CharacterAuthorityPoseChannelRuntimeBinding(
                    channelStart,
                    channelStart + 1,
                    "root/limb" + i + "/root",
                    0,
                    channelLabel: prefix + "root",
                    partLabel: "part." + i);
                channels[channelStart + 1] = new CharacterAuthorityPoseChannelRuntimeBinding(
                    channelStart + 1,
                    channelStart + 2,
                    "root/limb" + i + "/mid",
                    0,
                    channelLabel: prefix + "mid",
                    partLabel: "part." + i);
                channels[channelStart + 2] = new CharacterAuthorityPoseChannelRuntimeBinding(
                    channelStart + 2,
                    channelStart + 3,
                    "root/limb" + i + "/tip",
                    0,
                    channelLabel: prefix + "tip",
                    partLabel: "part." + i);
                CharacterAuthorityPoseQuantizedVector3 pole = poleOverride
                    ?? (mirrorPoles
                        ? i % 2 == 0
                            ? new CharacterAuthorityPoseQuantizedVector3(32767, 0, 0)
                            : new CharacterAuthorityPoseQuantizedVector3(-32767, 0, 0)
                        : new CharacterAuthorityPoseQuantizedVector3(0, 0, 32767));
                limbs[i] = new CharacterAuthorityPoseLimbRuntimeBinding(
                    ordinal: i,
                    probeId: i + 1,
                    ikBindingId: i + 1,
                    partStateId: partStateId,
                    handedness: i % 2 == 0
                        ? CharacterAuthorityPoseRigHandedness.Left
                        : CharacterAuthorityPoseRigHandedness.Right,
                    mirrorPeerProbeId: limbCount > 1 ? (i ^ 1) + 1 : 0,
                    rootChannelId: channelStart + 1,
                    midChannelId: channelStart + 2,
                    tipChannelId: channelStart + 3,
                    tipProbeOriginMillimeters: new CharacterAuthorityPoseQuantizedVector3(0, 0, 0),
                    tipProbeAxisSnorm: new CharacterAuthorityPoseQuantizedVector3(0, -32767, 0),
                    maxUpDistanceMillimeters: Math.Max(1000, rootYMillimeters),
                    maxDownDistanceMillimeters: Math.Max(1000, rootYMillimeters),
                    radiusMillimeters: 0,
                    clearanceMillimeters: 0,
                    staticLayerMask: 1 << GroundLayer,
                    minimumSlopeCosineMilli: 800,
                    poleDirectionSnorm: pole,
                    rootToMidLengthMillimeters: 1000,
                    midToTipLengthMillimeters: 1000,
                    minimumReachMillimeters: 100,
                    maximumReachMillimeters: 2000,
                    worldNormalTargetOffsetMillimeters: 0,
                    allowedMotionIds: allowedMotionIds ?? Array.Empty<string>(),
                    allowedPostureIds: allowedPostureIds ?? Array.Empty<int>(),
                    disabledActionTags: disabledActionTags ?? Array.Empty<string>(),
                    enterFrames,
                    exitFrames,
                    lostFrames);
            }
            return new CharacterAuthorityPoseRigRuntimeBinding(
                "mx.characterAuthorityPoseRig.v2",
                "2.0",
                definitionRevision,
                "sha256:" + definitionRevision.ToString("x64", System.Globalization.CultureInfo.InvariantCulture),
                "LeftHandedModelRoot",
                "Millimeter",
                32767,
                channels,
                limbs,
                Array.Empty<CharacterAuthorityPoseAttachmentRuntimeBinding>());
        }

        private static CharacterAuthorityPoseRigDefinition CreateDefinition(
            CharacterAuthorityPoseRigRuntimeBinding rig)
        {
            return CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(rig);
        }

        private static CharacterSocketBakeCatalog CreateCatalog(
            CharacterAuthorityPoseRigRuntimeBinding rig,
            int tipXOffsetMillimeters)
        {
            var catalog = new CharacterSocketBakeCatalog();
            for (int i = 0; i < rig.Channels.Length; i++)
            {
                CharacterAuthorityPoseChannelRuntimeBinding channel = rig.Channels[i];
                int limbIndex = channel.Ordinal / 3;
                int role = channel.Ordinal % 3;
                int centerX = rig.Limbs.Length == 1
                    ? 0
                    : ((limbIndex * 2) - (rig.Limbs.Length - 1)) * 1000;
                int rootY = rig.Limbs[limbIndex].MaxUpDistanceMillimeters;
                int x = centerX;
                int y = rootY;
                if (role == 1)
                    y = rootY / 2;
                else if (role == 2)
                    x += tipXOffsetMillimeters;
                catalog.RegisterTrack(
                    ClipKey,
                    channel.ChannelId,
                    1000,
                    new[]
                    {
                        new MxAnimationBakedSocketFrame(
                            0,
                            channel.ChannelId,
                            channel.BonePath,
                            new MxAnimationBakedVector3(x, y, 0),
                            new MxAnimationBakedVector3(0, 0, 0))
                    },
                    1000,
                    new[]
                    {
                        new CharacterSocketBakeBasisFrame(
                            0,
                            new MxAnimationBakedVector3(1000, 0, 0),
                            new MxAnimationBakedVector3(0, 1000, 0),
                            new MxAnimationBakedVector3(0, 0, 1000))
                    });
            }
            return catalog;
        }

        private static CharacterTestCharacterDefinition CreateCharacterDefinition()
        {
            return new CharacterTestCharacterDefinition(
                characterId: 9101,
                displayName: "Terrain IK Test",
                prefabKey: default(ResourceKey),
                defaultTeamId: 1,
                animationBinding: default);
        }

        internal static CombatPhysicsWorld CreateGroundWorld()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(8001);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(8001), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(8002),
                GroundLayer,
                new FixVector3(-Fix64.FromInt(32), -Fix64.One, -Fix64.FromInt(32)),
                new FixVector3(Fix64.FromInt(32), Fix64.Zero, Fix64.FromInt(32)),
                CombatSurfaceMaterial.Rough));
            return world;
        }

        private static void Tick(TerrainFixture fixture, CombatPhysicsWorld physics, long frame)
        {
            fixture.System.TickForTests(
                fixture.Entity,
                physics,
                new RuntimeFrame(frame),
                ClipKey,
                localFrame: 0,
                loop: true);
        }

        private static void AssertFailure(
            TerrainFixture fixture,
            CharacterTestAuthorityPoseFailure expected,
            long frame = 1L)
        {
            Tick(fixture, CreateGroundWorld(), frame);
            Assert.IsTrue(fixture.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                FirstTipChannelId,
                out CharacterTestResolvedAuthorityPoseSample sample));
            Assert.AreEqual(expected, sample.Failure);
            Assert.AreEqual(CharacterTestAuthorityPoseSampleSourceMask.Base, sample.SourceMask);
        }

        private static CharacterActionConfig FindTaggedAction(CharacterActionConfigRegistry registry)
        {
            CharacterActionConfig[] actions = registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i]?.Tags != null && actions[i].Tags.Length > 0)
                    return actions[i];
            }
            Assert.Fail("Character Action registry has no tagged action for eligibility proof.");
            return null;
        }

        private static CharacterTestCharacterDefinition FindDragonDefinition()
        {
            var definitions = CharacterTestPlayableCharacterCatalog.Discover();
            for (int i = 0; i < definitions.Count; i++)
            {
                string root = (definitions[i].GeneratedPackageRoot ?? string.Empty)
                    .Replace('\\', '/')
                    .TrimEnd('/');
                if (root.EndsWith("/000003", StringComparison.OrdinalIgnoreCase))
                    return definitions[i];
            }
            Assert.Fail("Playable Dragon14 definition is missing.");
            return default;
        }

        private static void SeverPart(
            CharacterTestCharacterEntity entity,
            int partStateId,
            long breakFrame,
            long severFrame)
        {
            CharacterTestBodyPartStateComponent state = entity.BodyPartState;
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(partStateId, out int index));
            Assert.IsTrue(state.TryGetDefinition(index, out CharacterTestBodyPartStateDefinition definition));
            Assert.IsTrue(state.TryBeginFrame(breakFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    breakFrame,
                    sourceEntityId: 77,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex: index,
                    finalDamage: int.MaxValue)));
            Assert.AreEqual(
                CharacterTestBodyPartStateCommitStatus.Committed,
                state.CommitFrame().Status);
            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    severFrame,
                    sourceEntityId: 77,
                    targetEntityId: entity.Id.Value,
                    definitionIndex: index,
                    severTag: definition.SeverRequiredTag,
                    correlationId: 2L)));
            Assert.AreEqual(
                CharacterTestBodyPartStateCommitStatus.Committed,
                state.CommitFrame().Status);
        }

        private static void AssertForwardAlongChild(FixVector3 actual, FixVector3 child)
        {
            Assert.IsTrue(child.TryNormalize(out FixVector3 expected));
            AssertVectorNear(actual, expected, toleranceRaw: 4L);
        }

        private static void AssertVectorNear(
            FixVector3 actual,
            FixVector3 expected,
            long toleranceRaw = 4L)
        {
            AssertRawNear(actual.X.RawValue, expected.X.RawValue, toleranceRaw);
            AssertRawNear(actual.Y.RawValue, expected.Y.RawValue, toleranceRaw);
            AssertRawNear(actual.Z.RawValue, expected.Z.RawValue, toleranceRaw);
        }

        private static void AssertRawNear(long actual, long expected, long toleranceRaw = 4L)
        {
            Assert.That(Abs(actual - expected), Is.LessThanOrEqualTo(toleranceRaw));
        }

        private static long Abs(long value) => value < 0L ? -value : value;

        private static FixVector3 AxisY() =>
            new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private static FixVector3 AxisZ() =>
            new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        internal sealed class TerrainFixture
        {
            internal TerrainFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterEntity entity,
                CharacterTestAuthorityPoseTerrainSystem system,
                CharacterAuthorityPoseRigDefinition definition,
                CharacterSocketBakeCatalog catalog,
                int baseSourceRevision,
                string baseSourceHash)
            {
                World = world;
                Entity = entity;
                System = system;
                Definition = definition;
                Catalog = catalog;
                BaseSourceRevision = baseSourceRevision;
                BaseSourceHash = baseSourceHash;
            }

            internal CharacterTestCharacterRuntimeWorld World { get; }
            internal CharacterTestCharacterEntity Entity { get; }
            internal CharacterTestAuthorityPoseTerrainSystem System { get; }
            internal CharacterAuthorityPoseRigDefinition Definition { get; }
            internal CharacterSocketBakeCatalog Catalog { get; }
            internal int BaseSourceRevision { get; }
            internal string BaseSourceHash { get; }
        }
    }
}
