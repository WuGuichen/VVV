using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestComponentWorldTests
    {
        [Test]
        public void Spawn_RegistersTypedStoresAndGameplayBridge()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: 101,
                    teamId: 2,
                    x: 1f,
                    y: 2f,
                    z: 3f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                CreateDefinition());

            Assert.AreEqual(1, world.EntityIds.Count);
            Assert.AreEqual(entity.Id.Value, world.EntityIds[0].Value);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterTransformComponent transform));
            Assert.AreEqual(1000, transform.XMillimeters);
            Assert.AreEqual(2000, transform.YMillimeters);
            Assert.AreEqual(3000, transform.ZMillimeters);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterActionComponent action));
            Assert.AreEqual(CharacterTestCharacterActionComponent.None.State, action.State);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterLifecycleComponent lifecycleState));
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, lifecycleState.LifeState);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestEntityPresenceComponent presence));
            Assert.AreEqual(CharacterTestEntityPresenceState.Active, presence.State);
            Assert.IsTrue(world.TryGetComponent(
                entity.Id,
                out CharacterTestSpecialHitStateComponent specialHitState));
            Assert.AreEqual(CharacterTestSpecialHitStateKind.None, specialHitState.State);
            Assert.IsTrue(world.TryGetComponent(
                entity.Id,
                out CharacterTestSpecialHitSusceptibilityComponent susceptibility));
            Assert.IsTrue(susceptibility.CanBeLaunched);
            Assert.IsTrue(susceptibility.RequiresBrokenForLaunch);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestColliderPostureComponent colliderPosture));
            Assert.AreEqual(CharacterTestColliderPostureComponent.StandingProfileId, colliderPosture.ProfileId);
            Assert.AreEqual(entity.Physics.EffectiveHeightMillimeters, colliderPosture.HeightMillimeters);
            Assert.AreEqual(entity.Physics.EffectiveRadiusMillimeters, colliderPosture.RadiusMillimeters);
            Assert.AreEqual(entity.Physics.BlocksCharacters, colliderPosture.BlocksCharacters);
            Assert.AreEqual(entity.Physics.CanBePushed, colliderPosture.CanBePushed);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out GameplayEntityId gameplayEntityId));
            Assert.AreEqual(entity.GameplayEntityId, gameplayEntityId);
            Assert.IsTrue(world.TryGetGameplayLifecycle(entity, out GameplayLifecycleComponent lifecycle));
            Assert.AreEqual(GameplayLifecycleState.Alive, lifecycle.State);
            Assert.IsTrue(
                world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<
                        GameplayTargetClassificationComponent>
                        classifications));
            Assert.IsTrue(
                classifications.TryGet(
                    entity.GameplayEntityId,
                    out GameplayTargetClassificationComponent
                        classification));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                classification.Capabilities);
        }

        [Test]
        public void EntityFacade_ReadsAndWritesTypedStores()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: 101,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());

            entity.SetTransform(new CharacterTestCharacterTransformComponent(9f, 8f, 7f));
            entity.SetCombatState(CharacterTestCharacterCombatStateComponent.CreateDefault().BeginDeath(10, 2, 3));
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                activeActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test_react"),
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Reaction,
                localFrame: 4,
                lastRejectReason: default,
                lastAnimationActionKey: "anim.previous",
                animationActionKeyThisFrame: "anim.reaction",
                animationTransitionFrames: 2,
                animationHoldFrames: 3,
                jumpImpulseYMillimetersPerSecond: 0));
            entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                Vector2.zero,
                locomotionSpeed01: 0f,
                grounded: true,
                airborne: false,
                actionActive: true,
                actionKey: "clip.damage"));

            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterTransformComponent storedTransform));
            Assert.AreEqual(entity.Transform.XMillimeters, storedTransform.XMillimeters);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterCombatStateComponent storedCombat));
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, storedCombat.LifeState);
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, entity.CombatState.LifeState);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterLifecycleComponent lifecycle));
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, lifecycle.LifeState);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterActionComponent storedAction));
            Assert.AreEqual("test_react", storedAction.ActiveActionId);
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterAnimationComponent storedAnimation));
            Assert.AreEqual("clip.damage", storedAnimation.ActionKey);

            Assert.IsTrue(world.SetComponent(
                entity.Id,
                new CharacterTestCharacterTransformComponent(2f, 4f, 6f)));
            Assert.AreEqual(2000, entity.Transform.XMillimeters);
            Assert.AreEqual(4000, entity.Transform.YMillimeters);
            Assert.AreEqual(6000, entity.Transform.ZMillimeters);
        }

        [Test]
        public void RemoveAndClear_RemoveTypedStoresAndGameplayState()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity first = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            CharacterTestCharacterEntity second = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    2,
                    1f,
                    0f,
                    0f,
                CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            GameplayEntityId firstGameplayEntityId = first.GameplayEntityId;

            Assert.IsTrue(world.Remove(first.Id));
            Assert.AreEqual(1, world.EntityIds.Count);
            Assert.IsFalse(world.TryGetComponent(first.Id, out CharacterTestCharacterCombatStateComponent _));
            Assert.IsFalse(world.TryGetComponent(first.Id, out CharacterTestEntityPresenceComponent _));
            Assert.IsFalse(world.TryGetComponent(first.Id, out CharacterTestSpecialHitStateComponent _));
            Assert.IsFalse(world.TryGetComponent(first.Id, out CharacterTestSpecialHitSusceptibilityComponent _));
            Assert.IsFalse(world.GameplayComponentWorld.IsAlive(firstGameplayEntityId));
            Assert.IsTrue(world.TryGetComponent(second.Id, out CharacterTestCharacterCombatStateComponent _));

            world.Clear();
            Assert.AreEqual(0, world.EntityIds.Count);
            Assert.AreEqual(0, world.Entities.Count);
            Assert.AreEqual(0, world.GameplayComponentWorld.CountAlive);
        }

        [Test]
        public void RequestBuffers_AreStoredPerEntity()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity first = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            CharacterTestCharacterEntity second = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());

            first.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                velocityXMillimetersPerSecond: 100,
                velocityYMillimetersPerSecond: 0,
                velocityZMillimetersPerSecond: 0,
                movementMode: CharacterMovementMode.Airborne,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0072));

            Assert.AreEqual(1, world.GetMutableRequestBuffer(first.Id).MotionRequests.Count);
            Assert.AreEqual(0, world.GetMutableRequestBuffer(second.Id).MotionRequests.Count);
            Assert.AreEqual(1, first.PendingMotionRequestCount);
            Assert.AreEqual(0, second.PendingMotionRequestCount);
        }

        [Test]
        public void ColliderPosture_SetPhysicsPreservesActionTimelineOverride()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            entity.SetColliderPosture(entity.ColliderPosture.WithActionOverride(
                CharacterTestColliderPostureComponent.LowProfileId,
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                transitionFrames: 2,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("slide"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0061));

            entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                capsuleRadiusMillimeters: 320,
                capsuleHeightMillimeters: 1900,
                capsuleCenterYMillimeters: 950,
                massGrams: 80000,
                massScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                radiusScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                pushScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                blocksCharacters: true,
                canBePushed: true));

            CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
            Assert.AreEqual(CharacterTestColliderPostureComponent.StandingProfileId, posture.ProfileId);
            Assert.AreEqual(1900, posture.HeightMillimeters);
            Assert.AreEqual(320, posture.RadiusMillimeters);
            Assert.AreEqual(CharacterTestColliderPostureComponent.LowProfileId, posture.ActionProfileId);
            Assert.AreEqual(CharacterActionCollisionProfileRestorePolicy.OnActionEnd, posture.ActionRestorePolicy);
            Assert.AreEqual("slide", posture.ActionSourceActionId);
            Assert.AreEqual("slide.collision_low", posture.ActionStableEventId);
        }

        [Test]
        public void CollisionProfileResolver_DeadCorpsePostureKeepsLegalCapsuleDimensions()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                activeActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("death_react"),
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Reaction,
                localFrame: 12,
                lastRejectReason: default,
                lastAnimationActionKey: string.Empty,
                animationActionKeyThisFrame: string.Empty,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0));
            entity.SetColliderPosture(entity.ColliderPosture.WithActionOverride(
                CharacterTestColliderPostureComponent.CorpseProfileId,
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                transitionFrames: 0,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("death_react"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0022));

            var resolver = new CharacterTestCollisionProfileResolverSystem();
            resolver.Tick(world, physicsRuntime: null, new RuntimeFrame(12), traceRecorder: null);

            CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
            Assert.AreEqual(CharacterTestColliderPostureComponent.CorpseProfileId, posture.ProfileId);
            Assert.IsFalse(posture.BlocksCharacters);
            Assert.IsFalse(posture.CanBePushed);
            Assert.GreaterOrEqual(posture.HeightMillimeters, posture.RadiusMillimeters * 2);
        }

        [Test]
        public void CollisionProfileResolver_PostureResolveDoesNotAllocateAfterWarmup()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            entity.SetColliderPosture(entity.ColliderPosture.WithActionOverride(
                CharacterTestColliderPostureComponent.LowProfileId,
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                transitionFrames: 0,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("slide"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0061));
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                activeActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("slide"),
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Action,
                localFrame: 3,
                lastRejectReason: default,
                lastAnimationActionKey: string.Empty,
                animationActionKeyThisFrame: string.Empty,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0));
            var resolver = new CharacterTestCollisionProfileResolverSystem();

            for (int i = 0; i < 8; i++)
                resolver.Tick(world, physicsRuntime: null, new RuntimeFrame(i), traceRecorder: null);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                resolver.Tick(world, physicsRuntime: null, new RuntimeFrame(100 + i), traceRecorder: null);
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(CharacterTestColliderPostureComponent.LowProfileId, entity.ColliderPosture.ProfileId);
        }

        [Test]
        public void ColliderPosture_InvalidEnumValuesFallbackWithoutAllocation()
        {
            var invalidSource = (CharacterTestColliderPostureSource)999;
            var invalidPolicy = (CharacterActionCollisionProfileRestorePolicy)999;
            CharacterTestColliderPostureComponent posture = default;

            for (int i = 0; i < 8; i++)
            {
                posture = new CharacterTestColliderPostureComponent(
                    CharacterTestColliderPostureComponent.StandingProfileId,
                    heightMillimeters: 1800,
                    radiusMillimeters: 300,
                    centerYMillimeters: 900,
                    blocksCharacters: true,
                    canBePushed: true,
                    source: invalidSource,
                    priority: 0,
                    sinceFrame: i,
                    actionRestorePolicy: invalidPolicy);
            }

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
            {
                posture = new CharacterTestColliderPostureComponent(
                    CharacterTestColliderPostureComponent.StandingProfileId,
                    heightMillimeters: 1800,
                    radiusMillimeters: 300,
                    centerYMillimeters: 900,
                    blocksCharacters: true,
                    canBePushed: true,
                    source: invalidSource,
                    priority: 0,
                    sinceFrame: i,
                    actionRestorePolicy: invalidPolicy);
            }
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(CharacterTestColliderPostureSource.Standing, posture.Source);
            Assert.AreEqual(
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                posture.ActionRestorePolicy);
        }

        [Test]
        public void NetworkSnapshot_RoundTripsColliderPosture()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                CreateDefinition());
            entity.SetColliderPosture(new CharacterTestColliderPostureComponent(
                CharacterTestColliderPostureComponent.CorpseProfileId,
                heightMillimeters: 396,
                radiusMillimeters: 260,
                centerYMillimeters: 198,
                blocksCharacters: false,
                canBePushed: false,
                source: CharacterTestColliderPostureSource.Lifecycle,
                priority: 90,
                sinceFrame: 24,
                actionProfileId: 0,
                actionRestorePolicy: CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                actionTransitionFrames: 0,
                actionSourceActionId: MxFramework.CharacterAction.CharacterActionId.None,
                actionStableEventId: 0));

            CharacterTestNetworkCharacterSnapshot snapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(entity);
            CharacterTestColliderPostureComponent restored =
                snapshot.CreateColliderPostureComponent(entity.Physics);

            Assert.AreEqual(CharacterTestColliderPostureComponent.CorpseProfileId, snapshot.ColliderProfileId);
            Assert.AreEqual(CharacterTestColliderPostureComponent.CorpseProfileId, restored.ProfileId);
            Assert.AreEqual(396, restored.HeightMillimeters);
            Assert.AreEqual(260, restored.RadiusMillimeters);
            Assert.AreEqual(198, restored.CenterYMillimeters);
            Assert.IsFalse(restored.BlocksCharacters);
            Assert.IsFalse(restored.CanBePushed);
            Assert.AreEqual(CharacterTestColliderPostureSource.Lifecycle, restored.Source);
            Assert.AreEqual(90, restored.Priority);
            Assert.AreEqual(24, restored.SinceFrame);
        }

        [Test]
        public void ActionRuntime_BoundWorldStartsAndTicksByEntityId()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            runtime.BindRuntimeWorld(world);

            Assert.IsTrue(runtime.TryStartActionSlot(
                entity.Id,
                CharacterTestActionSlotIds.AttackPrimary,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                RuntimeFrame.Zero,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0004)));
            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterActionComponent started));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, started.ActiveActionId);
            Assert.AreEqual(started.ActiveActionId, entity.Action.ActiveActionId);

            runtime.TickEntity(entity.Id, clearFrameRequests: false);

            Assert.IsTrue(world.TryGetComponent(entity.Id, out CharacterTestCharacterActionComponent ticked));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, ticked.ActiveActionId);
            Assert.AreEqual(ticked.LocalFrame, entity.Action.LocalFrame);
        }

        [Test]
        public void ActionRuntime_CohesionBreakRejectedByInterruptRulesKeepsActionAndComponent()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                CreateDefinition());
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            runtime.BindRuntimeWorld(world);

            Assert.IsTrue(runtime.TryStartActionSlot(
                entity.Id,
                CharacterTestActionSlotIds.AttackPrimary,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                RuntimeFrame.Zero,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0005)));
            runtime.TickEntity(entity.Id, clearFrameRequests: false);
            runtime.TickEntity(entity.Id, clearFrameRequests: false);
            Assert.IsTrue(runtime.TryCreateRunnerRestoreState(
                entity.Id,
                out CharacterActionExecutionState before));
            CharacterTestCharacterActionComponent beforeComponent = entity.Action;
            CharacterReactionContext context = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(2),
                    entity.GameplayEntityId,
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20,
                    sourceId: 7,
                    reason: new GameplayScatterPressureReason(1201304),
                    traceId: new RuntimeTraceIdentity(983115))).Context;

            bool reacted = runtime.TryReact(entity.Id, context, allowFallback: false);

            Assert.IsFalse(reacted);
            Assert.IsTrue(runtime.TryCreateRunnerRestoreState(
                entity.Id,
                out CharacterActionExecutionState after));
            Assert.AreEqual(before.ActiveActionInstanceId, after.ActiveActionInstanceId);
            Assert.AreEqual(before.ActionId, after.ActionId);
            Assert.AreEqual(before.LocalFrame, after.LocalFrame);
            Assert.AreEqual(before.State, after.State);
            Assert.AreEqual(beforeComponent.ActiveActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(beforeComponent.LocalFrame, entity.Action.LocalFrame);
            Assert.AreEqual(beforeComponent.State, entity.Action.State);
            Assert.AreEqual(
                CharacterActionRunnerEventKind.InterruptRejected,
                runtime.TimelineEvents[runtime.TimelineEvents.Count - 1].Kind);
        }

        [Test]
        public void NetworkSnapshot_RestoresActionRunnerStateAndContinuesNextFrame()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            runtime.BindRuntimeWorld(world);

            Assert.IsTrue(runtime.TryStartActionSlot(
                entity.Id,
                CharacterTestActionSlotIds.AttackPrimary,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                RuntimeFrame.Zero,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0006)));

            runtime.TickEntity(entity.Id, clearFrameRequests: false);
            runtime.TickEntity(entity.Id, clearFrameRequests: false);

            Assert.IsTrue(runtime.TryCreateRunnerRestoreState(
                entity.Id,
                out CharacterActionExecutionState captured));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, captured.ActionId);
            Assert.AreEqual(2, captured.LocalFrame);

            CharacterTestNetworkCharacterSnapshot snapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(entity, runtime);
            Assert.AreEqual(captured.ActiveActionInstanceId, snapshot.ActionRunnerActiveInstanceId);
            Assert.AreEqual(captured.NextInstanceId, snapshot.ActionRunnerNextInstanceId);
            Assert.AreEqual(captured.LocalFrame, snapshot.ActionRunnerLocalFrame);

            var restoredWorld = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity restoredEntity = restoredWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());
            CharacterTestCharacterActionRuntime restoredRuntime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            restoredRuntime.BindRuntimeWorld(restoredWorld);

            CharacterActionExecutionState restoreState = snapshot.CreateActionExecutionState();
            Assert.IsTrue(restoredRuntime.TryRestoreRunner(
                restoredEntity.ComponentWorld,
                restoredEntity.Id,
                restoreState,
                out string diagnostic), diagnostic);

            Assert.IsTrue(restoredRuntime.TryCreateRunnerRestoreState(
                restoredEntity.Id,
                out CharacterActionExecutionState restored));
            Assert.AreEqual(captured.ActiveActionInstanceId, restored.ActiveActionInstanceId);
            Assert.AreEqual(captured.NextInstanceId, restored.NextInstanceId);
            Assert.AreEqual(captured.ActionId, restored.ActionId);
            Assert.AreEqual(captured.LocalFrame, restored.LocalFrame);
            Assert.AreEqual(captured.LocalFrame, restoredEntity.Action.LocalFrame);

            restoredRuntime.TickEntity(restoredEntity.Id, clearFrameRequests: false);

            Assert.IsTrue(restoredRuntime.TryCreateRunnerRestoreState(
                restoredEntity.Id,
                out CharacterActionExecutionState afterTick));
            Assert.AreEqual(captured.LocalFrame + 1, afterTick.LocalFrame);
            Assert.AreEqual(afterTick.LocalFrame, restoredEntity.Action.LocalFrame);
        }

        [Test]
        public void BattleState_DefaultAliveOpensCombatActionInputAndPlannerGates()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                CreateDefinition());

            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(10), null);

            CharacterTestBattleStateComponent battleState = entity.BattleState;
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, battleState.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.None, battleState.DominantReason);
            Assert.IsTrue(battleState.CanBeTargeted);
            Assert.IsTrue(battleState.CanReceiveHpDamage);
            Assert.IsTrue(battleState.CanReceiveScatter);
            Assert.IsTrue(battleState.CanStartNormalAction);
            Assert.IsTrue(battleState.CanMoveByInput);
            Assert.IsTrue(battleState.CanRunRuntimeAiPlanner);
            Assert.IsFalse(battleState.KeepsTerminalPose);
        }

        [Test]
        public void BattleState_LifecycleTerminalStatesCloseCombatActionInputAndPlannerGates()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());

            entity.BeginDeath(frame: 20, sourceEntityId: 2, traceId: 9);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(20), null);
            AssertClosedTerminalBattleState(entity.BattleState, CharacterTestCharacterLifeState.Dying);

            entity.CompleteDeath(50);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(50), null);
            AssertClosedTerminalBattleState(entity.BattleState, CharacterTestCharacterLifeState.Dead);

            entity.BeginRevive(frame: 70, health: 30);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(70), null);
            CharacterTestBattleStateComponent reviving = entity.BattleState;
            Assert.AreEqual(CharacterTestCharacterLifeState.Reviving, reviving.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.Revive, reviving.DominantReason);
            Assert.IsFalse(reviving.CanBeTargeted);
            Assert.IsFalse(reviving.CanReceiveHpDamage);
            Assert.IsFalse(reviving.CanReceiveScatter);
            Assert.IsFalse(reviving.CanStartNormalAction);
            Assert.IsFalse(reviving.CanMoveByInput);
            Assert.IsFalse(reviving.CanRunRuntimeAiPlanner);
            Assert.IsFalse(reviving.KeepsTerminalPose);
        }

        [Test]
        public void BattleState_BrokenAliveAllowsHpDamageButBlocksScatterDebt()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());
            entity.SetCombatState(entity.CombatState.WithScatterPressure(
                scatterPressure: 100,
                maxScatterPressure: 100,
                scatterPressureBand: PressureBand.Broken,
                brokenRecoveryDelayFrames: 270,
                lastCohesionBreakFrame: 12));

            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(13), null);

            CharacterTestBattleStateComponent battleState = entity.BattleState;
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, battleState.LifeState);
            Assert.IsTrue(battleState.CanBeTargeted);
            Assert.IsTrue(battleState.CanReceiveHpDamage);
            Assert.IsFalse(battleState.CanReceiveScatter);
            Assert.IsTrue(battleState.CanStartNormalAction);
            Assert.IsTrue(battleState.CanMoveByInput);
        }

        [Test]
        public void BattleState_LaunchedAliveKeepsHpDamageButBlocksActionInputAndPlanner()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());
            entity.SetCombatState(entity.CombatState.WithScatterPressure(
                scatterPressure: 100,
                maxScatterPressure: 100,
                scatterPressureBand: PressureBand.Broken,
                brokenRecoveryDelayFrames: 270,
                lastCohesionBreakFrame: 12));
            entity.SetSpecialHitState(CharacterTestSpecialHitStateComponent.Launched(
                frame: 20,
                sourceEntityId: 2,
                traceId: 30,
                launchVelocityYMillimetersPerSecond: 7500,
                horizontalImpulseMillimetersPerSecond: 0));

            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(20), null);

            CharacterTestBattleStateComponent battleState = entity.BattleState;
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, battleState.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.Launch, battleState.DominantReason);
            Assert.IsTrue(battleState.CanBeTargeted);
            Assert.IsTrue(battleState.CanReceiveHpDamage);
            Assert.IsFalse(battleState.CanReceiveScatter);
            Assert.IsFalse(battleState.CanStartNormalAction);
            Assert.IsFalse(battleState.CanMoveByInput);
            Assert.IsFalse(battleState.CanRunRuntimeAiPlanner);
        }

        [Test]
        public void BattleState_PendingDestroyClosesAllGatesAndDoesNotCountAlive()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                CreateDefinition());

            entity.SetPresence(entity.Presence.WithPendingDestroy(
                frame: 40,
                sourceEntityId: 3,
                traceId: new RuntimeTraceIdentity(91010001, 3, 40)));
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(40), null);

            CharacterTestBattleStateComponent battleState = entity.BattleState;
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, battleState.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.Destroy, battleState.DominantReason);
            Assert.IsFalse(battleState.CanBeTargeted);
            Assert.IsFalse(battleState.CanReceiveHpDamage);
            Assert.IsFalse(battleState.CanReceiveScatter);
            Assert.IsFalse(battleState.CanStartNormalAction);
            Assert.IsFalse(battleState.CanMoveByInput);
            Assert.IsFalse(battleState.CanRunRuntimeAiPlanner);
            Assert.IsFalse(battleState.KeepsTerminalPose);
            Assert.IsFalse(battleState.CountsAsAlive);
        }

        [Test]
        public void BattleState_ReactionActionBlocksNormalActionInputAndPlannerButKeepsHpDamage()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    101,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                CreateDefinition());
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                CharacterTestCharacterActionConfigSource.KatanaCohesionBreakReactionActionId,
                MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Reaction,
                localFrame: 0,
                lastRejectReason: default,
                lastAnimationActionKey: string.Empty,
                animationActionKeyThisFrame: string.Empty,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0,
                movementMode: CharacterMovementMode.ControlLocked,
                movementLocked: true));

            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(30), null);

            CharacterTestBattleStateComponent battleState = entity.BattleState;
            Assert.AreEqual(CharacterTestBattleStateDominantReason.CohesionBreakReaction, battleState.DominantReason);
            Assert.IsTrue(battleState.CanBeTargeted);
            Assert.IsTrue(battleState.CanReceiveHpDamage);
            Assert.IsTrue(battleState.CanReceiveScatter);
            Assert.IsFalse(battleState.CanStartNormalAction);
            Assert.IsFalse(battleState.CanMoveByInput);
            Assert.IsFalse(battleState.CanRunRuntimeAiPlanner);
        }

        private static void AssertClosedTerminalBattleState(
            CharacterTestBattleStateComponent battleState,
            CharacterTestCharacterLifeState lifeState)
        {
            Assert.AreEqual(lifeState, battleState.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.Death, battleState.DominantReason);
            Assert.IsFalse(battleState.CanBeTargeted);
            Assert.IsFalse(battleState.CanReceiveHpDamage);
            Assert.IsFalse(battleState.CanReceiveScatter);
            Assert.IsFalse(battleState.CanStartNormalAction);
            Assert.IsFalse(battleState.CanMoveByInput);
            Assert.IsFalse(battleState.CanRunRuntimeAiPlanner);
            Assert.IsTrue(battleState.KeepsTerminalPose);
        }

        private static CharacterTestCharacterDefinition CreateDefinition()
        {
            return new CharacterTestCharacterDefinition(
                characterId: 101,
                displayName: "Store Test",
                prefabKey: default(ResourceKey),
                defaultTeamId: 1,
                animationBinding: default);
        }
    }
}
