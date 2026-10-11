using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestDeathReviveTests
    {
        [TestCase(930001, "art.character.skeleton.animation.damage_ko_mid_2")]
        [TestCase(930102, "art.character.skeleton.animation.damage_ko_mid_2")]
        [TestCase(950004, "art.character.combat_girl_sword.animation.s_die")]
        public void SharedDeathActionsResolveExplicitAnimationClipsInSourceAndRuntimeProjection(int actionId, string expectedClip)
        {
            foreach (string path in new[] {
                "CharacterActionSource/Libraries/Common/character_actions.json",
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json" })
            {
                var imported = CharacterActionJsonImporter.ImportActions(System.IO.File.ReadAllText(path));
                Assert.IsTrue(imported.Success);
                CharacterActionConfig action = Array.Find(imported.Actions, value => value.ActionId == new CharacterActionId(actionId));
                Assert.IsNotNull(action);
                Assert.IsNotNull(action.AnimationTrack);
                Assert.AreEqual(1, action.AnimationTrack.Events.Length);
                Assert.AreEqual(expectedClip, action.AnimationTrack.Events[0].AnimationClipResourceKey,
                    "A lifecycle action must select its authored clip instead of relying on an unbound semantic alias.");
            }
        }

        [Test]
        public void SceneProfile_DefaultEnablesTraceFileWriteForUnitySessions()
        {
            CharacterTestSceneProfile profile = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            try
            {
                Assert.IsTrue(profile.EnableTraceFileWrite);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CombatState_DeathClearsScatterAndHealthDoesNotAutoRevive()
        {
            CharacterTestCharacterCombatStateComponent state = CharacterTestCharacterCombatStateComponent
                .CreateDefault()
                .WithScatterPressure(120, 100, PressureBand.Broken, 270, 10)
                .BeginDeath(frame: 20, sourceEntityId: 3, traceId: 99);

            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, state.LifeState);
            Assert.AreEqual(0, state.Health);
            Assert.AreEqual(0, state.ScatterPressure);
            Assert.AreEqual(PressureBand.Stable, state.ScatterPressureBand);
            Assert.IsFalse(state.IsAlive);
            Assert.IsFalse(state.TryGetCohesionBreakRecoveryProgress(30, out _));

            CharacterTestCharacterCombatStateComponent dead = state.CompleteDeath(80);
            CharacterTestCharacterCombatStateComponent healed = dead.WithHealth(dead.MaxHealth, 30);

            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, healed.LifeState);
            Assert.AreEqual(30, healed.Health);
            Assert.IsFalse(healed.IsAlive);
        }

        [Test]
        public void HitTraceWindowState_DeathExitAfterFrameStartClosesActiveTraceBeforeClear()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            var state = new CharacterTestHitTraceWindowState();
            var frame = new RuntimeFrame(20);

            entity.EnqueueCombatRequest(CreateHitTraceWindowRequest(
                frame,
                entity.Id.Value,
                CharacterActionTrackLifecyclePhase.Enter,
                CharacterActionTrackLifecycleBoundary.BeforeFrame));
            state.ProcessStartCombatRequests(world, frame, null);
            Assert.AreEqual(1, state.ActiveTraceCount);

            entity.EnqueueCombatRequest(CreateHitTraceWindowRequest(
                frame,
                entity.Id.Value,
                CharacterActionTrackLifecyclePhase.Exit,
                CharacterActionTrackLifecycleBoundary.BeforeFrame));
            state.ProcessStopCombatRequestsAndClear(world, frame, null);

            Assert.AreEqual(0, state.ActiveTraceCount);
            Assert.AreEqual(0, entity.PendingCombatRequestCount);
        }

        [Test]
        public void HitTraceWindowState_DyingSourceCannotKeepActiveTrace()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            var state = new CharacterTestHitTraceWindowState();
            var frame = new RuntimeFrame(30);

            entity.EnqueueCombatRequest(CreateHitTraceWindowRequest(
                frame,
                entity.Id.Value,
                CharacterActionTrackLifecyclePhase.Enter,
                CharacterActionTrackLifecycleBoundary.BeforeFrame));
            state.ProcessStartCombatRequests(world, frame, null);
            Assert.AreEqual(1, state.ActiveTraceCount);

            entity.BeginDeath(frame.Value, sourceEntityId: 2, traceId: 90);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, frame, null);
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(world);
            state.RemoveEndedTraces(lookup);

            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, entity.BattleState.LifeState);
            Assert.AreEqual(0, state.ActiveTraceCount);
        }

        [Test]
        public void CombatState_ExplicitReviveReturnsAliveAfterLifecycleCompletes()
        {
            CharacterTestCharacterCombatStateComponent dead = CharacterTestCharacterCombatStateComponent
                .CreateDefault()
                .BeginDeath(frame: 10, sourceEntityId: 2, traceId: 30)
                .CompleteDeath(70);

            CharacterTestCharacterCombatStateComponent reviving = dead.BeginRevive(frame: 120, health: 30);
            Assert.AreEqual(CharacterTestCharacterLifeState.Reviving, reviving.LifeState);
            Assert.AreEqual(30, reviving.Health);
            Assert.IsFalse(reviving.IsAlive);

            CharacterTestCharacterCombatStateComponent alive = reviving.CompleteRevive(150);
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, alive.LifeState);
            Assert.AreEqual(30, alive.Health);
            Assert.IsTrue(alive.IsAlive);
        }

        [Test]
        public void ReviveCharacterCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.ReviveCharacter(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 7,
                reviveHp: 25,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestRuntimeCommandIds.ReviveCharacter,
                targetId: 7,
                payload0: 25,
                payload1: CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion + 1,
                payload2: 0,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejected = validator.Validate(invalidSchema);
            CharacterTestReviveRequest request = CharacterTestRuntimeCommandFactory.DecodeReviveCharacter(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(7, request.TargetEntityId);
            Assert.AreEqual(25, request.ReviveHp);
            Assert.AreEqual(CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion, request.SchemaVersion);
        }

        [Test]
        public void DestroyCharacterCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.DestroyCharacter(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 7,
                sourceEntityId: 3,
                traceId: new RuntimeTraceIdentity(984903));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestRuntimeCommandIds.DestroyCharacter,
                targetId: 7,
                payload0: 3,
                payload1: CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion + 1,
                payload2: 0,
                traceId: new RuntimeTraceIdentity(984903));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejected = validator.Validate(invalidSchema);
            CharacterTestDestroyCharacterRequest request =
                CharacterTestRuntimeCommandFactory.DecodeDestroyCharacter(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(7, request.TargetEntityId);
            Assert.AreEqual(3, request.SourceEntityId);
            Assert.AreEqual(CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion, request.SchemaVersion);
            Assert.AreEqual("test.destroy", request.TraceId);
        }

        [Test]
        public void EmbeddedReactionProfile_SelectsDeathActionWithoutFallback()
        {
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(CreateEmbeddedOnlyActionOptions());
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);

            CharacterReactionContextBuildResult context = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(33),
                entity.GameplayEntityId,
                "hp_zero",
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0007));

            bool started = runtime.TryReact(
                entity,
                context.Context,
                allowFallback: false);
            if (!started)
            {
                Assert.Fail("death reaction failed: reject="
                    + entity.Action.LastRejectReason
                    + " active="
                    + entity.Action.ActiveActionId);
            }
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                entity.Action.ActiveActionId);
            if (!runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                out CharacterActionConfig deathAction))
            {
                Assert.Fail("missing death action: "
                    + CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId
                    + " actionCount="
                    + runtime.Registry.Actions.Length);
            }
            Assert.AreEqual(1, deathAction.AnimationTrack.Events.Length);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DeathReactionAnimationActionKey,
                deathAction.AnimationTrack.Events[0].AnimationActionKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DeathReactionAnimationClipResourceKey,
                deathAction.AnimationTrack.Events[0].AnimationClipResourceKey);
        }

        [Test]
        public void DefaultExternalReactionProfile_SelectsDeathActionWithoutFallback()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);

            CharacterReactionContextBuildResult context = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(33),
                entity.GameplayEntityId,
                "hp_zero",
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0007));

            bool started = runtime.TryReact(
                entity,
                context.Context,
                allowFallback: false);

            Assert.IsTrue(started, entity.Action.LastRejectReason);
            Assert.AreEqual(
                "codex_flow_test.sword_empty.reaction.death",
                entity.Action.ActiveActionId);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                entity.Action.ActiveActionId,
                out CharacterActionConfig deathAction));
            Assert.AreEqual(1, deathAction.AnimationTrack.Events.Length);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DeathReactionAnimationClipResourceKey,
                deathAction.AnimationTrack.Events[0].AnimationClipResourceKey);
        }

        [Test]
        public void EmbeddedReactionProfile_SelectsLaunchActionWithoutFallback()
        {
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(CreateEmbeddedOnlyActionOptions());
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            CharacterReactionContextBuildResult context = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(40),
                    entity.GameplayEntityId,
                    reactionGroupId: CharacterTestReactionGroupIds.SpecialLaunch,
                    sourceId: 2,
                    traceId: new RuntimeTraceIdentity(917549),
                    previousPressureBand: PressureBand.Broken,
                    currentScatterPressureBand: PressureBand.Broken,
                    previousPressure: 100,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));

            bool started = runtime.TryReact(
                entity,
                context.Context,
                allowFallback: false);

            Assert.IsTrue(started, entity.Action.LastRejectReason);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaLaunchReactionActionId,
                entity.Action.ActiveActionId);
            AssertLaunchAction(runtime);
        }

        [Test]
        public void DefaultExternalReactionProfile_SelectsLaunchActionWithoutFallback()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            CharacterReactionContextBuildResult context = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(40),
                    entity.GameplayEntityId,
                    reactionGroupId: CharacterTestReactionGroupIds.SpecialLaunch,
                    sourceId: 2,
                    traceId: new RuntimeTraceIdentity(917549),
                    previousPressureBand: PressureBand.Broken,
                    currentScatterPressureBand: PressureBand.Broken,
                    previousPressure: 100,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));

            bool started = runtime.TryReact(
                entity,
                context.Context,
                allowFallback: false);

            Assert.IsTrue(started, entity.Action.LastRejectReason);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaLaunchReactionActionId,
                entity.Action.ActiveActionId);
            AssertLaunchAction(runtime);
        }

        [Test]
        public void HitReactionProfiles_SelectHeightAndStrengthForExternalAndEmbeddedSources()
        {
            CharacterTestCharacterActionRuntime[] runtimes =
            {
                CharacterTestCharacterActionConfigSource.Load(),
                CharacterTestCharacterActionConfigSource.Load(CreateEmbeddedOnlyActionOptions()),
            };

            for (int i = 0; i < runtimes.Length; i++)
            {
                CharacterTestCharacterActionRuntime runtime = runtimes[i];
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightHigh,
                    impactForce: 59,
                    (CharacterTestCharacterActionConfigSource.KatanaHitHighReactionActionId).ToString());
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightMid,
                    impactForce: 59,
                    (CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId).ToString());
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightLow,
                    impactForce: 59,
                    (CharacterTestCharacterActionConfigSource.KatanaHitLowReactionActionId).ToString());
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightHigh,
                    impactForce: 60,
                    (CharacterTestCharacterActionConfigSource.KatanaHitHighStrongReactionActionId).ToString());
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightMid,
                    impactForce: 60,
                    (CharacterTestCharacterActionConfigSource.KatanaHitMidStrongReactionActionId).ToString());
                AssertHitReactionSelection(
                    runtime,
                    CharacterTestReactionGroupIds.HeightLow,
                    impactForce: 60,
                    (CharacterTestCharacterActionConfigSource.KatanaHitLowStrongReactionActionId).ToString());
            }
        }

        [Test]
        public void RuntimeModule_ReviveStartsReviveActionAndCompletesAlive()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                actionConfigOptions: CreateEmbeddedOnlyActionOptions(),
                createViews: false);
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(module.World);
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            entity.CompleteDeath(70);
            module.World.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
            module.World.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);
            AssertGameplayLifecycle(module.World, entity, GameplayLifecycleState.Destroyed);

            bool accepted = module.TryReviveCharacter(
                new CharacterTestReviveRequest(
                    entity.Id.Value,
                    reviveHp: 0,
                    CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion),
                new RuntimeFrame(100),
                out string message);

            Assert.IsTrue(accepted, message);
            Assert.AreEqual(30, entity.CombatState.Health);
            Assert.AreEqual(CharacterTestCharacterLifeState.Reviving, entity.CombatState.LifeState);
            Assert.AreEqual(CharacterTestCharacterLifeState.Reviving, entity.BattleState.LifeState);
            Assert.IsFalse(entity.BattleState.CanReceiveHpDamage);
            Assert.IsFalse(entity.BattleState.CanStartNormalAction);
            AssertGameplayLifecycle(module.World, entity, GameplayLifecycleState.Destroyed);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaReviveReactionActionId,
                entity.Action.ActiveActionId);

            for (int frame = 101; frame <= 131; frame++)
                module.Tick(new RuntimeTickContext(frame, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, entity.CombatState.LifeState);
            Assert.IsTrue(entity.CombatState.IsAlive);
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, entity.BattleState.LifeState);
            Assert.IsTrue(entity.BattleState.CanReceiveHpDamage);
            Assert.IsTrue(entity.BattleState.CanStartNormalAction);
            AssertGameplayLifecycle(module.World, entity, GameplayLifecycleState.Alive);
            Assert.AreEqual(30, module.World.GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Hp));
        }

        [Test]
        public void RuntimeModule_DeathActionCompletesDeadAndKeepsGameplayLifecycleDestroyed()
        {
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory();
            var module = new CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                actionConfigOptions: CreateEmbeddedOnlyActionOptions(),
                traceRecorder: traceRecorder,
                createViews: false);
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(module.World);
            module.World.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
            module.World.ClearScatterPressure(entity, frame: 10);
            module.World.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            CharacterReactionContextBuildResult reactionContext =
                CharacterReactionContextBuilder.FromDeath(
                    new RuntimeFrame(10),
                    entity.GameplayEntityId,
                    "hp_zero",
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0008));

            bool started = module.ActionRuntime.TryReact(
                entity,
                reactionContext.Context,
                allowFallback: false);

            Assert.IsTrue(started, entity.Action.LastRejectReason);
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, entity.CombatState.LifeState);
            AssertGameplayLifecycle(module.World, entity, GameplayLifecycleState.Destroyed);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                entity.Action.ActiveActionId);

            for (int frame = 11; frame <= 80; frame++)
                module.Tick(new RuntimeTickContext(frame, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, entity.CombatState.LifeState);
            Assert.IsFalse(entity.CombatState.IsAlive);
            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, entity.BattleState.LifeState);
            Assert.IsTrue(entity.BattleState.KeepsTerminalPose);
            Assert.IsFalse(entity.BattleState.CanReceiveHpDamage);
            Assert.IsFalse(entity.BattleState.CanStartNormalAction);
            AssertGameplayLifecycle(module.World, entity, GameplayLifecycleState.Destroyed);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                entity.Action.ActiveActionId);
            AssertDeathAnimationKey(entity.Action.LastAnimationActionKey);
            Assert.IsTrue(entity.Animation.ActionActive);
            Assert.AreEqual(string.Empty, entity.Animation.ActionKey);
            Assert.That(
                traceRecorder.RecentEvents,
                Has.Some.Matches<CharacterTestTraceEvent>(
                    evt => evt.Message.Contains("death complete")));

            for (int frame = 81; frame <= 85; frame++)
                module.Tick(new RuntimeTickContext(frame, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, entity.CombatState.LifeState);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                entity.Action.ActiveActionId);
            Assert.IsTrue(entity.Animation.ActionActive);
        }

        [Test]
        public void CharacterActionSystem_PreservesPackageDeathTerminalHoldWithoutKatanaIds()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            entity.CompleteDeath(frame: 70);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(70), null);
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("codex_flow_test.sword_empty.reaction.death"),
                MxFramework.Game.CharacterTest.CharacterTestActionStateId.Completed,
                MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Death,
                localFrame: 60,
                lastRejectReason: default,
                lastAnimationActionKey: "reaction.death",
                animationActionKeyThisFrame: string.Empty,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0,
                CharacterMovementMode.Idle,
                movementLocked: true));
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                CreateEmbeddedOnlyActionOptions());

            system.Tick(world, new RuntimeFrame(71));

            Assert.AreEqual(
                "codex_flow_test.sword_empty.reaction.death",
                entity.Action.ActiveActionId);
            Assert.AreEqual("reaction.death", entity.Action.LastAnimationActionKey);
            Assert.IsTrue(CharacterTestCharacterActionSystem.IsDeathTerminalActionHold(entity));
        }

        [Test]
        public void CharacterAnimationSync_DeadPackageActionOverridesStunFallback()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            entity.CompleteDeath(frame: 70);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(70), null);
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("codex_flow_test.sword_empty.reaction.death"),
                MxFramework.Game.CharacterTest.CharacterTestActionStateId.Completed,
                MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Death,
                localFrame: 60,
                lastRejectReason: default,
                lastAnimationActionKey: "reaction.death",
                animationActionKeyThisFrame: string.Empty,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0,
                CharacterMovementMode.Idle,
                movementLocked: true));
            new CharacterTestCharacterAnimationSystem().Tick(world, new RuntimeFrame(71));
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            var animationSync = new CharacterTestCharacterAnimationSyncSystem(null);

            CharacterTestCharacterAnimationComponent animation =
                animationSync.ResolvePresentationAnimationForValidation(
                    entity,
                    statusControl,
                    out bool presentationPaused);

            Assert.IsFalse(presentationPaused);
            Assert.IsTrue(animation.ActionActive);
            Assert.AreEqual(string.Empty, animation.ActionKey);
        }

        [Test]
        public void CharacterActionSystem_DeathLifecycleAdvancesWhileStunned()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(10), null);
            CharacterReactionContextBuildResult reactionContext =
                CharacterReactionContextBuilder.FromDeath(
                    new RuntimeFrame(10),
                    entity.GameplayEntityId,
                    "hp_zero",
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0008));
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                CreateEmbeddedOnlyActionOptions());
            Assert.IsTrue(system.Runtime.TryReact(
                entity,
                reactionContext.Context,
                allowFallback: false));
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);

            for (int frame = 11; frame <= 80; frame++)
                system.Tick(world, new RuntimeFrame(frame), statusControl);

            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, entity.BattleState.LifeState);
            Assert.IsTrue(CharacterTestCharacterActionSystem.IsDeathTerminalActionHold(entity));
        }

        [Test]
        public void ControlEffectBridges_IgnoreStatusesAfterDeathBegins()
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, new RuntimeFrame(10), null);
            var statusBridge = new CharacterTestStatusControlEffectBridge();
            var motionBridge = new CharacterTestMechanicMotionEffectBridge();

            statusBridge.Tick(world);
            motionBridge.Tick(world);

            Assert.AreEqual(0, statusBridge.LastActionFrozenCount);
            Assert.IsFalse(statusBridge.IsActionFrozen(entity));
            Assert.AreEqual(0, motionBridge.LastAppliedPolicyCount);
            Assert.AreEqual(0, entity.PendingMotionRequestCount);
        }

        [Test]
        public void RuntimeModule_DeadCanBeDestroyedAndRemovedFromWorlds()
        {
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory();
            var module = new CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                actionConfigOptions: CreateEmbeddedOnlyActionOptions(),
                traceRecorder: traceRecorder,
                createViews: false);
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(module.World);
            GameplayEntityId gameplayEntityId = entity.GameplayEntityId;
            entity.BeginDeath(frame: 10, sourceEntityId: 2, traceId: 50);
            entity.CompleteDeath(70);
            module.World.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);

            bool accepted = module.TryDestroyCharacter(
                new CharacterTestDestroyCharacterRequest(
                    entity.Id.Value,
                    sourceEntityId: 2,
                    CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0009)),
                new RuntimeFrame(100),
                out string message);

            Assert.IsTrue(accepted, message);
            Assert.AreEqual(CharacterTestEntityPresenceState.PendingDestroy, entity.Presence.State);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.Destroy, entity.BattleState.DominantReason);
            Assert.IsFalse(entity.BattleState.CanBeTargeted);
            Assert.IsFalse(entity.BattleState.CanReceiveHpDamage);
            Assert.IsFalse(entity.BattleState.CanStartNormalAction);
            Assert.AreEqual(1, module.World.Entities.Count);

            module.Tick(new RuntimeTickContext(101, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, module.World.Entities.Count);
            Assert.AreEqual(0, module.World.EntityIds.Count);
            Assert.IsFalse(module.World.TryGetComponent(
                entity.Id,
                out CharacterTestEntityPresenceComponent _));
            Assert.IsFalse(module.World.GameplayComponentWorld.IsAlive(gameplayEntityId));
            Assert.AreEqual(0, module.World.CombatGameplayMap.Count);
            Assert.That(
                traceRecorder.RecentEvents,
                Has.Some.Matches<CharacterTestTraceEvent>(
                    evt => evt.Message.Contains("destroy requested")));
            Assert.That(
                traceRecorder.RecentEvents,
                Has.Some.Matches<CharacterTestTraceEvent>(
                    evt => evt.Message.Contains("destroy applied")));
        }

        [Test]
        public void RuntimeModule_DestroyRejectsAliveDyingRevivingAndPendingDestroyCannotRevive()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                actionConfigOptions: CreateEmbeddedOnlyActionOptions(),
                createViews: false);

            CharacterTestCharacterEntity alive = SpawnDefaultCharacter(module.World);
            Assert.IsFalse(module.TryDestroyCharacter(
                CreateDestroyRequest(alive.Id.Value),
                new RuntimeFrame(10),
                out _));
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, alive.CombatState.LifeState);

            CharacterTestCharacterEntity dying = SpawnDefaultCharacter(module.World);
            dying.BeginDeath(frame: 12, sourceEntityId: 2, traceId: 60);
            CharacterTestBattleStateSystem.RefreshEntity(module.World, dying, new RuntimeFrame(12), null);
            Assert.IsFalse(module.TryDestroyCharacter(
                CreateDestroyRequest(dying.Id.Value),
                new RuntimeFrame(12),
                out _));
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, dying.CombatState.LifeState);

            CharacterTestCharacterEntity reviving = SpawnDefaultCharacter(module.World);
            reviving.BeginDeath(frame: 20, sourceEntityId: 2, traceId: 70);
            reviving.CompleteDeath(50);
            reviving.BeginRevive(frame: 60, health: 30);
            CharacterTestBattleStateSystem.RefreshEntity(module.World, reviving, new RuntimeFrame(60), null);
            Assert.IsFalse(module.TryDestroyCharacter(
                CreateDestroyRequest(reviving.Id.Value),
                new RuntimeFrame(60),
                out _));
            Assert.AreEqual(CharacterTestCharacterLifeState.Reviving, reviving.CombatState.LifeState);

            CharacterTestCharacterEntity dead = SpawnDefaultCharacter(module.World);
            dead.BeginDeath(frame: 70, sourceEntityId: 2, traceId: 80);
            dead.CompleteDeath(90);
            Assert.IsTrue(module.TryDestroyCharacter(
                CreateDestroyRequest(dead.Id.Value),
                new RuntimeFrame(100),
                out string destroyMessage),
                destroyMessage);
            Assert.IsFalse(module.TryReviveCharacter(
                new CharacterTestReviveRequest(
                    dead.Id.Value,
                    reviveHp: 30,
                    CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion),
                new RuntimeFrame(101),
                out _));
        }

        private static CharacterTestCharacterActionConfigSourceOptions CreateEmbeddedOnlyActionOptions()
        {
            return CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback();
        }

        private static CharacterTestDestroyCharacterRequest CreateDestroyRequest(int entityId)
        {
            return new CharacterTestDestroyCharacterRequest(
                entityId,
                sourceEntityId: 2,
                CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0009));
        }

        private static CharacterTestCharacterEntity SpawnDefaultCharacter(
            CharacterTestCharacterRuntimeWorld world)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));

            return world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
        }

        private static CharacterActionCombatRequest CreateHitTraceWindowRequest(
            RuntimeFrame frame,
            int sourceEntityId,
            CharacterActionTrackLifecyclePhase lifecyclePhase,
            CharacterActionTrackLifecycleBoundary lifecycleBoundary)
        {
            return new CharacterActionCombatRequest(
                new CharacterActionAdapterDispatchMetadata(
                    frame,
                    instanceId: 100L,
                    planId: 200L,
                    actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.death_interrupt_attack"),
                    localFrame: (int)frame.Value,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0068,
                    sourceId: sourceEntityId,
                    traceId: new RuntimeTraceIdentity(973496),
                    lifecyclePhase: lifecyclePhase,
                    lifecycleBoundary: lifecycleBoundary),
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + sourceEntityId),
                CharacterActionTrackEventKind.HitTraceWindow,
                combatActionId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0067,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0081,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1);
        }

        private static void AssertGameplayLifecycle(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            GameplayLifecycleState expected)
        {
            Assert.IsTrue(world.TryGetGameplayLifecycle(entity, out GameplayLifecycleComponent lifecycle));
            Assert.AreEqual(expected, lifecycle.State);
            Assert.AreEqual(expected == GameplayLifecycleState.Alive, lifecycle.IsAlive);
        }

        private static void AssertDeathAnimationKey(string animationKey)
        {
            Assert.IsTrue(
                string.Equals(
                    animationKey,
                    CharacterTestCharacterActionConfigSource.DeathReactionAnimationActionKey,
                    StringComparison.Ordinal)
                || string.Equals(
                    animationKey,
                    CharacterTestCharacterActionConfigSource.DeathReactionAnimationClipResourceKey,
                    StringComparison.Ordinal),
                "Expected death animation key but was: " + animationKey);
        }

        private static void AssertLaunchAction(CharacterTestCharacterActionRuntime runtime)
        {
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.KatanaLaunchReactionActionId,
                out CharacterActionConfig launchAction));
            Assert.AreEqual(24, launchAction.DurationFrames);
            bool foundControlLock = false;
            MotionTrackEvent[] events = launchAction.MotionTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                MotionTrackEvent evt = events[i];
                Assert.AreNotEqual(
                    CharacterActionTrackEventKind.ApplyImpulse,
                    evt.Kind,
                    "Launch velocity is resolved from launchPower at runtime, not fixed in the action.");
                if (evt.Kind == CharacterActionTrackEventKind.SetControlScale
                    && evt.MovementMode == CharacterMovementMode.ControlLocked)
                {
                    foundControlLock = true;
                }
            }

            Assert.IsTrue(foundControlLock, "Expected launch action to lock control during the launch reaction.");
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LaunchReactionAnimationClipResourceKey,
                launchAction.AnimationTrack.Events[0].AnimationClipResourceKey);
        }

        private static void AssertHitReactionSelection(
            CharacterTestCharacterActionRuntime runtime,
            int reactionGroupId,
            int impactForce,
            string expectedActionId)
        {
            CharacterTestCharacterRuntimeWorld world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnDefaultCharacter(world);
            CharacterReactionContextBuildResult context = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(40),
                    entity.GameplayEntityId,
                    bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10,
                    hitZoneId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0051,
                    damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                    impactForce: impactForce,
                    reactionGroupId: reactionGroupId,
                    sourceId: 2,
                    traceId: new RuntimeTraceIdentity(964739),
                    previousPressureBand: PressureBand.Stable,
                    currentScatterPressureBand: PressureBand.Pressed,
                    previousPressure: 0,
                    currentScatterPressure: 34,
                    maxScatterPressure: 100));

            bool started = runtime.TryReact(
                entity,
                context.Context,
                allowFallback: false);

            Assert.IsTrue(started, entity.Action.LastRejectReason);
            Assert.AreEqual(expectedActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(0, entity.Action.MovementControlScaleMilli);
            Assert.AreEqual(0, entity.Action.RotationControlScaleMilli);
            runtime.TerminateAndReleaseEntityAction(
                entity,
                new RuntimeFrame(41),
                CharacterActionLifecycleReason.EntityDespawn,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0010));
        }
    }
}
