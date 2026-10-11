using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLargeCharacterPresenceContactTests
    {
        [Test]
        public void SingleMultipleAndBidirectionalVolumes_UseCanonicalPairAndDirectedKeys()
        {
            PairRunResult single = RunPair(
                new[] { SphereAt("a", 1f, 0.7f) },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>());
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, single.Status);
            Assert.AreEqual(1, single.PairCount);
            Assert.AreEqual(1, single.ConstraintCount);
            Assert.Greater(single.TargetVelocityX, 0);

            PairRunResult multiple = RunPair(
                new[]
                {
                    SphereAt("a", 0.9f, 0.8f),
                    SphereAt("b", 1.1f, 0.8f)
                },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>());
            Assert.AreEqual(1, multiple.PairCount);
            Assert.AreEqual(2, multiple.ConstraintCount);
            Assert.Greater(multiple.TargetVelocityX, 0);

            PairRunResult bidirectional = RunPair(
                new[] { SphereAt("a", 1f, 0.7f) },
                new[] { SphereAt("a", -1f, 0.7f) });
            Assert.AreEqual(1, bidirectional.PairCount, "A/B directed queries must share one unordered pair.");
            Assert.AreEqual(2, bidirectional.ConstraintCount, "Each owner/leaf/target direction remains explicit.");
            Assert.Greater(bidirectional.TargetVelocityX, 0);
        }

        [Test]
        public void ThreeCharacterChain_IsStableWhenRosterOrderIsReversed()
        {
            ChainRunResult forward = RunChain(reverseRoster: false);
            ChainRunResult reverse = RunChain(reverseRoster: true);

            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, forward.Status);
            Assert.AreEqual(forward.Status, reverse.Status);
            Assert.AreEqual(2, forward.PairCount);
            Assert.AreEqual(2, forward.ConstraintCount);
            Assert.AreEqual(forward.PairCount, reverse.PairCount);
            Assert.AreEqual(forward.ConstraintCount, reverse.ConstraintCount);
            Assert.AreEqual(forward.SecondVelocityX, reverse.SecondVelocityX);
            Assert.AreEqual(forward.ThirdVelocityX, reverse.ThirdVelocityX);
            Assert.Greater(forward.SecondVelocityX, 0);
            Assert.Greater(forward.ThirdVelocityX, 0);
        }

        [Test]
        public void SameCenterAndTouching_AreObservedWithoutDirectDisplacement()
        {
            PairRunResult sameCenter = RunPair(
                new[] { SphereAt("a", 0f, 0.5f) },
                new[] { SphereAt("a", 0f, 0.5f) },
                targetX: 0f,
                attemptedVelocityX: 0);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, sameCenter.Status);
            Assert.AreEqual(1, sameCenter.PairCount);
            Assert.AreEqual(2, sameCenter.ConstraintCount);
            Assert.Greater(sameCenter.SourceContactCount, 0);
            Assert.Greater(sameCenter.TargetContactCount, 0);
            Assert.IsTrue(sameCenter.TransformsUnchanged);

            PairRunResult touching = RunPair(
                new[] { SphereAt("a", 1f, 0.66f) },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                targetX: 2f,
                attemptedVelocityX: 0);
            Assert.AreEqual(1, touching.PairCount);
            Assert.AreEqual(1, touching.ConstraintCount);
            Assert.Greater(touching.TargetContactCount, 0);
            Assert.IsTrue(touching.TransformsUnchanged);
        }

        [Test]
        public void SelfDeadAndUnpushable_AreExcludedOrObservedWithoutTargetVelocity()
        {
            CharacterTestCharacterPhysicsRuntime selfPhysics = CreatePhysics();
            var selfPresence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity self = CreateEntity(1, 0f);
            CharacterTestCharacterEntity[] selfRoster = { self };
            Assert.IsTrue(selfPresence.SyncAll(
                selfRoster,
                selfPhysics,
                new[] { CreateBinding(1, SphereAt("a", 0f, 1f)) }));
            var selfResolver = new CharacterTestCharacterContactResolver(null);
            selfResolver.ResolveCharacterContactPressure(selfRoster, selfPhysics, bodyPresenceReady: true);
            selfResolver.ApplyCharacterContactState(selfRoster);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, selfResolver.LastBodyPresenceContactBuildStatus);
            Assert.AreEqual(0, selfResolver.LastBodyPresenceConstraintCount);
            Assert.AreEqual(0, self.Physics.ContactCount);

            CharacterTestCharacterPhysicsRuntime deadPhysics = CreatePhysics();
            var deadPresence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity dead = CreateEntity(1, 0f);
            CharacterTestCharacterEntity alive = CreateEntity(2, 2f);
            CharacterTestCharacterEntity[] deadRoster = { dead, alive };
            Assert.IsTrue(deadPresence.SyncAll(
                deadRoster,
                deadPhysics,
                new[]
                {
                    CreateBinding(1, SphereAt("a", 1f, 0.7f)),
                    CreateBinding(1)
                }));
            SetDead(dead);
            var deadResolver = new CharacterTestCharacterContactResolver(null);
            deadResolver.ResolveCharacterContactPressure(deadRoster, deadPhysics, bodyPresenceReady: true);
            deadResolver.ApplyCharacterContactState(deadRoster);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, deadResolver.LastBodyPresenceContactBuildStatus);
            Assert.AreEqual(0, deadResolver.LastBodyPresenceConstraintCount);
            Assert.AreEqual(0, alive.Physics.ContactCount);

            PairRunResult unpushable = RunPair(
                new[] { SphereAt("a", 1f, 0.7f) },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                targetCanBePushed: false);
            Assert.AreEqual(1, unpushable.ConstraintCount);
            Assert.Greater(unpushable.TargetContactCount, 0);
            Assert.AreEqual(0, unpushable.TargetVelocityX);
            Assert.IsTrue(unpushable.TransformsUnchanged);
        }

        [Test]
        public void BlocksCharactersControlsPresenceEligibilityWhileCanBePushedOnlyControlsPressure()
        {
            PairRunResult nonBlocking = RunPair(
                new[] { SphereAt("a", 1f, 0.7f) },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                targetCanBePushed: false,
                targetBlocksCharacters: false);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, nonBlocking.Status);
            Assert.AreEqual(0, nonBlocking.PairCount);
            Assert.AreEqual(0, nonBlocking.ConstraintCount);
            Assert.AreEqual(0, nonBlocking.SourceContactCount);
            Assert.AreEqual(0, nonBlocking.TargetContactCount);
            Assert.AreEqual(0, nonBlocking.TargetVelocityX);

            PairRunResult blockingUnpushable = RunPair(
                new[] { SphereAt("a", 1f, 0.7f) },
                Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                targetCanBePushed: false,
                targetBlocksCharacters: true);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, blockingUnpushable.Status);
            Assert.AreEqual(1, blockingUnpushable.PairCount);
            Assert.AreEqual(1, blockingUnpushable.ConstraintCount);
            Assert.Greater(blockingUnpushable.TargetContactCount, 0);
            Assert.AreEqual(0, blockingUnpushable.TargetVelocityX);
        }

        [Test]
        public void RawCandidateOverflow_FailsClosedBeforePresenceMutationAndRootPressureContinues()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity[] entities = new CharacterTestCharacterEntity[8];
            CharacterImportedGeometryBinding[] bindings = new CharacterImportedGeometryBinding[8];
            CharacterImportedGeometryBinding fourVolumes = CreateBinding(1, CreateFourLargeSpheres());
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = CreateEntity(i + 1, 0f);
                bindings[i] = fourVolumes;
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            var foreignBodyId = new CombatBodyId(700000);
            physics.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(700000),
                foreignBodyId,
                FixVector3.Zero));
            physics.World.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                foreignBodyId,
                new CombatColliderId(1),
                CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer,
                new FixVector3(Fix64.Zero, Meters(0.93f), Fix64.Zero),
                Fix64.One));

            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.RecordAttemptedContactVelocity(entities[0].Id.Value, 1000, 0);
            resolver.ResolveCharacterContactPressure(entities, physics, bodyPresenceReady: true);
            resolver.ApplyCharacterContactState(entities);

            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded, resolver.LastBodyPresenceContactBuildStatus);
            Assert.AreEqual(29, resolver.LastBodyPresenceRawCandidateCount);
            Assert.AreEqual(0, resolver.LastBodyPresencePairCount);
            Assert.AreEqual(0, resolver.LastBodyPresenceConstraintCount);
            Assert.Greater(entities[1].Physics.ContactVelocityXMillimetersPerSecond, 0,
                "The existing Hard Root pressure path must continue after Presence rejection.");
        }

        [Test]
        public void WarmedEightByFourCandidateConstraintAndPressureLoop_AllocatesZeroBytes()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity[] entities = new CharacterTestCharacterEntity[8];
            CharacterImportedGeometryBinding[] bindings = new CharacterImportedGeometryBinding[8];
            CharacterImportedGeometryBinding fourVolumes = CreateBinding(1, CreateFourLargeSpheres());
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = CreateEntity(i + 1, i * 2f);
                bindings[i] = fourVolumes;
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            var resolver = new CharacterTestCharacterContactResolver(null);
            for (int i = 0; i < 8; i++)
                RunPressureFrame(resolver, entities, physics);

            Assert.AreEqual(28, resolver.LastBodyPresencePairCount);
            Assert.AreEqual(224, resolver.LastBodyPresenceConstraintCount);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allSucceeded = true;
            for (int i = 0; i < 128; i++)
            {
                RunPressureFrame(resolver, entities, physics);
                allSucceeded &= resolver.LastBodyPresenceContactBuildStatus
                    == CharacterTestBodyPresenceContactBuildStatus.Success;
                allSucceeded &= resolver.LastBodyPresencePairCount == 28;
                allSucceeded &= resolver.LastBodyPresenceConstraintCount == 224;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(allSucceeded);
            Assert.AreEqual(0L, allocated);
        }

        private static PairRunResult RunPair(
            CharacterBodyPresenceRuntimeBinding[] sourceVolumes,
            CharacterBodyPresenceRuntimeBinding[] targetVolumes,
            float targetX = 2f,
            int attemptedVelocityX = 1000,
            bool targetCanBePushed = true,
            bool targetBlocksCharacters = true)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, targetX);
            if (!targetCanBePushed)
                SetCanBePushed(target, canBePushed: false);
            if (!targetBlocksCharacters)
                SetBlocksCharacters(target, blocksCharacters: false);
            CharacterTestCharacterEntity[] entities = { source, target };
            Assert.IsTrue(presence.SyncAll(
                entities,
                physics,
                new[]
                {
                    CreateBinding(1, sourceVolumes),
                    CreateBinding(1, targetVolumes)
                }));

            int sourceX = source.Transform.XMillimeters;
            int sourceZ = source.Transform.ZMillimeters;
            int targetXBefore = target.Transform.XMillimeters;
            int targetZ = target.Transform.ZMillimeters;
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.RecordAttemptedContactVelocity(source.Id.Value, attemptedVelocityX, 0);
            resolver.ResolveCharacterContactPressure(entities, physics, bodyPresenceReady: true);
            resolver.ApplyCharacterContactState(entities);
            return new PairRunResult(
                resolver.LastBodyPresenceContactBuildStatus,
                resolver.LastBodyPresencePairCount,
                resolver.LastBodyPresenceConstraintCount,
                source.Physics.ContactCount,
                target.Physics.ContactCount,
                target.Physics.ContactVelocityXMillimetersPerSecond,
                source.Transform.XMillimeters == sourceX
                    && source.Transform.ZMillimeters == sourceZ
                    && target.Transform.XMillimeters == targetXBefore
                    && target.Transform.ZMillimeters == targetZ);
        }

        private static ChainRunResult RunChain(bool reverseRoster)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity first = CreateEntity(1, 0f);
            CharacterTestCharacterEntity second = CreateEntity(2, 2f);
            CharacterTestCharacterEntity third = CreateEntity(3, 4f);
            CharacterTestCharacterEntity[] entities = { first, second, third };
            CharacterImportedGeometryBinding[] bindings =
            {
                CreateBinding(1, SphereAt("a", 1f, 0.7f)),
                CreateBinding(1, SphereAt("a", 1f, 0.7f)),
                CreateBinding(1)
            };
            if (reverseRoster)
            {
                Array.Reverse(entities);
                Array.Reverse(bindings);
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.RecordAttemptedContactVelocity(first.Id.Value, 1000, 0);
            resolver.ResolveCharacterContactPressure(entities, physics, bodyPresenceReady: true);
            resolver.ApplyCharacterContactState(entities);
            return new ChainRunResult(
                resolver.LastBodyPresenceContactBuildStatus,
                resolver.LastBodyPresencePairCount,
                resolver.LastBodyPresenceConstraintCount,
                second.Physics.ContactVelocityXMillimetersPerSecond,
                third.Physics.ContactVelocityXMillimetersPerSecond);
        }

        private static void RunPressureFrame(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterEntity[] entities,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            resolver.Clear();
            resolver.RecordAttemptedContactVelocity(entities[0].Id.Value, 1000, 0);
            resolver.ResolveCharacterContactPressure(entities, physics, bodyPresenceReady: true);
            resolver.ApplyCharacterContactState(entities);
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "test.body-presence-contact");
        }

        private static CharacterTestCharacterEntity CreateEntity(int id, float x)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "Presence Contact Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId: 1, defaultTeamId: 1),
                new CharacterTestCharacterTransformComponent(x, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static void SetCanBePushed(CharacterTestCharacterEntity entity, bool canBePushed)
        {
            CharacterTestCharacterPhysicsComponent physics = entity.Physics.WithRuntimeModifiers(
                massScale: 1f,
                radiusScale: 1f,
                pushScale: 1f,
                canBePushed: canBePushed);
            entity.SetPhysics(physics);
            entity.SetColliderPosture(CharacterTestColliderPostureComponent.FromPhysics(physics, frame: 0L));
        }

        private static void SetBlocksCharacters(
            CharacterTestCharacterEntity entity,
            bool blocksCharacters)
        {
            entity.SetPhysics(entity.Physics.WithRuntimeModifiers(
                massScale: 1f,
                radiusScale: 1f,
                pushScale: 1f,
                blocksCharacters: blocksCharacters));
        }

        private static void SetDead(CharacterTestCharacterEntity entity)
        {
            entity.ComponentWorld.SetBattleState(
                entity.Id,
                new CharacterTestBattleStateComponent(
                    CharacterTestCharacterLifeState.Dead,
                    CharacterTestBattleStateDominantReason.Death,
                    CharacterTestBattleGateFlags.None,
                    sinceFrame: 1L,
                    lastTransitionFrame: 1L,
                    sourceActionId: CharacterActionId.None,
                    sourceTraceId: 0));
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            int revision,
            params CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            return new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
                "test.body-presence-contact",
                null,
                Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                Array.Empty<CharacterHitZoneRuntimeBinding>(),
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                Array.Empty<CharacterSocketRuntimeBinding>(),
                Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPresenceVolumes: volumes,
                bodyPresenceRevision: revision,
                bodyPresenceContentHash: Hash(revision),
                bodyPresenceMode: CharacterBodyPresenceMode.Authored);
        }

        private static CharacterBodyPresenceRuntimeBinding SphereAt(
            string volumeId,
            float x,
            float radius)
        {
            return new CharacterBodyPresenceRuntimeBinding(
                volumeId,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                CharacterBodyPresenceShape.Sphere,
                new CharacterRuntimePose(
                    "ModelRoot",
                    string.Empty,
                    new CharacterRuntimeVector3(x, 0.93f, 0f),
                    new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                    new CharacterRuntimeVector3(1f, 1f, 1f)),
                radius,
                height: 0f,
                size: default,
                skinMillimeters: 0,
                pressureScaleMilli: 1000,
                enabled: true);
        }

        private static CharacterBodyPresenceRuntimeBinding[] CreateFourLargeSpheres()
        {
            return new[]
            {
                SphereAt("a", 0f, 20f),
                SphereAt("b", 0f, 20f),
                SphereAt("c", 0f, 20f),
                SphereAt("d", 0f, 20f)
            };
        }

        private static string Hash(int revision)
        {
            char digit = (char)('0' + revision % 10);
            return "sha256:" + new string(digit, 64);
        }

        private static Fix64 Meters(float value)
        {
            return CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                CharacterTestDeterministicUnits.MetersToMillimeters(value));
        }

        private readonly struct PairRunResult
        {
            public PairRunResult(
                CharacterTestBodyPresenceContactBuildStatus status,
                int pairCount,
                int constraintCount,
                int sourceContactCount,
                int targetContactCount,
                int targetVelocityX,
                bool transformsUnchanged)
            {
                Status = status;
                PairCount = pairCount;
                ConstraintCount = constraintCount;
                SourceContactCount = sourceContactCount;
                TargetContactCount = targetContactCount;
                TargetVelocityX = targetVelocityX;
                TransformsUnchanged = transformsUnchanged;
            }

            public CharacterTestBodyPresenceContactBuildStatus Status { get; }
            public int PairCount { get; }
            public int ConstraintCount { get; }
            public int SourceContactCount { get; }
            public int TargetContactCount { get; }
            public int TargetVelocityX { get; }
            public bool TransformsUnchanged { get; }
        }

        private readonly struct ChainRunResult
        {
            public ChainRunResult(
                CharacterTestBodyPresenceContactBuildStatus status,
                int pairCount,
                int constraintCount,
                int secondVelocityX,
                int thirdVelocityX)
            {
                Status = status;
                PairCount = pairCount;
                ConstraintCount = constraintCount;
                SecondVelocityX = secondVelocityX;
                ThirdVelocityX = thirdVelocityX;
            }

            public CharacterTestBodyPresenceContactBuildStatus Status { get; }
            public int PairCount { get; }
            public int ConstraintCount { get; }
            public int SecondVelocityX { get; }
            public int ThirdVelocityX { get; }
        }
    }
}
