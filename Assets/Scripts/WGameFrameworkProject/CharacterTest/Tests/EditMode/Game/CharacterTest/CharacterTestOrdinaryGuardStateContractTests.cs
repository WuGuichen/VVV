using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Animation;
using MxFramework.Runtime;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace MxFramework.Tests.Game.CharacterTest
{
    // Verifies the existing state/resolve contract before adding ordinary-guard content.
    // This does not stand in for action Enter/Exit, death, or presentation integration.
    public sealed class CharacterTestOrdinaryGuardStateContractTests
    {
        private static readonly CombatEntityId Target = new CombatEntityId(2);

        [TestCase(0, -1, 0, 0, -1)]
        [TestCase(0, 0, 90000, 1, -1)]
        [TestCase(1, 0, 180000, 1, -2)]
        public void BakedIncomingMotionIncludesAuthorityTranslationAndFacing(int x, int z, int yaw, int dx, int dz)
        {
            var baked = StaticBakedFrame();
            var binding = new CharacterTestWeaponTraceBinding(1, 1f, .1f);
            var previous = new CharacterTestDirectHitTraceSourcePose(0, FixVector3.Zero, 0);
            var current = new CharacterTestDirectHitTraceSourcePose(1,
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.FromInt(z)), yaw);
            var queryGeometry = CharacterTestBakedWeaponTraceProvider.BuildTraceFrame(1, baked, binding,
                current, FixVector3.Zero, CombatPhysicsLayerMask.All);
            var motion = CharacterTestBakedWeaponTraceProvider.BuildIncomingMotionFrame(1, baked, binding,
                previous, FixVector3.Zero, current, FixVector3.Zero, CombatPhysicsLayerMask.All);
            Assert.AreEqual(FixVector3.Zero, queryGeometry.TipNow - queryGeometry.TipPrev,
                "The old current-pose projection deliberately excludes root motion.");
            var expected = new FixVector3(Fix64.FromInt(dx), Fix64.Zero, Fix64.FromInt(dz));
            Assert.AreEqual(expected, motion.TipNow - motion.TipPrev);
            var queries = new List<CombatCapsuleQuery>();
            var samples = new List<CharacterTestDirectHitTracePlanSample>();
            var bounds = new CharacterTestDirectHitTraceBounds(); int queryId = 0, order = 0;
            CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(queryGeometry, new CombatEntityId(1), 301,
                1, 1, 0, 1, true, Fix64.One, 8, ref queryId, ref order, queries, samples, ref bounds, motion);
            Assert.AreEqual(expected, samples.Single().ResolveIncomingDisplacement(queryGeometry.TipNow));
            Assert.AreEqual(queryGeometry.TipNow, samples.Single().Query.PointA,
                "Incoming motion must not silently redefine the existing collision query.");
        }

        [Test]
        public void BakedIncomingMotionIncludesBothAuthoritySocketOffsetsAndUnknownWitnessStaysUnknown()
        {
            var pose = new CharacterTestDirectHitTraceSourcePose(0, FixVector3.Zero, 0);
            var binding = new CharacterTestWeaponTraceBinding(1, 1f, .1f);
            var motion = CharacterTestBakedWeaponTraceProvider.BuildIncomingMotionFrame(1, StaticBakedFrame(), binding,
                pose, new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One), pose, FixVector3.Zero, CombatPhysicsLayerMask.All);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One), motion.TipNow - motion.TipPrev);
            var query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(motion, new CombatEntityId(1), 301, 0, 0);
            var missing = new CharacterTestDirectHitTracePlanSample(query, CharacterTestDirectHitTraceSampleKind.WeaponTraceTipSweep,
                1, 0, 0, 1, 301, 1, 1, 0, 1, default(WeaponTraceFrame));
            Assert.AreEqual(FixVector3.Zero, missing.ResolveIncomingDisplacement(FixVector3.Zero),
                "Missing witness may not fall back to a spatial query axis.");
        }

        [Test]
        public void MotionWitnessIsRebuiltFromAuthorityAndExpiresAcrossFramesWorldsAndRelease()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var entity = SpawnWitnessEntity(world);
            var provider = new CharacterTestBakedWeaponTraceProvider(new EmptyBakeSource(), new EmptyBindingResolver(), null);
            var baked = StaticBakedFrame(); var binding = new CharacterTestWeaponTraceBinding(1, 1f, .1f);
            Assert.IsFalse(provider.TryGetIncomingMotionFrame(entity, 7, 1, baked, binding, CombatPhysicsLayerMask.All, out _));
            provider.CaptureSourceMotionStart(world, new RuntimeFrame(7));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, -1f));
            Assert.IsTrue(provider.TryGetIncomingMotionFrame(entity, 7, 1, baked, binding, CombatPhysicsLayerMask.All, out var motion));
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One), motion.TipNow - motion.TipPrev);
            Assert.IsFalse(provider.TryGetIncomingMotionFrame(entity, 8, 1, baked, binding, CombatPhysicsLayerMask.All, out _));
            var replacement = SpawnWitnessEntity(new CharacterTestCharacterRuntimeWorld());
            Assert.AreEqual(entity.Id, replacement.Id);
            Assert.IsFalse(provider.TryGetIncomingMotionFrame(replacement, 7, 1, baked, binding, CombatPhysicsLayerMask.All, out _));
            // A replay/restore repeats capture from its restored authority, including repeated frame ids.
            provider.CaptureSourceMotionStart(world, new RuntimeFrame(7));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, -2f));
            Assert.IsTrue(provider.TryGetIncomingMotionFrame(entity, 7, 1, baked, binding, CombatPhysicsLayerMask.All, out motion));
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One), motion.TipNow - motion.TipPrev);
            provider.ClearSourceMotion();
            Assert.IsFalse(provider.TryGetIncomingMotionFrame(entity, 7, 1, baked, binding, CombatPhysicsLayerMask.All, out _));
        }

        private static CharacterTestCharacterEntity SpawnWitnessEntity(CharacterTestCharacterRuntimeWorld world)
            => world.Spawn(new CharacterTestCharacterSpawnRequest(characterId: 101, teamId: 1, x: 0, y: 0, z: 0,
                controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                new CharacterTestCharacterDefinition(characterId: 101, displayName: "guard witness", prefabKey: default,
                    defaultTeamId: 1, animationBinding: default));

        private sealed class EmptyBakeSource : ICharacterTestBakedWeaponTraceSource
        {
            public bool TryGetFrame(string clip, int profile, int local, out MxAnimationBakedWeaponTraceFrame frame) { frame = null; return false; }
            public bool TryGetSocketFrame(string clip, int socket, int local, out MxAnimationBakedSocketFrame frame) { frame = null; return false; }
            public bool TryResolveSocketTrack(string clip, int socket, out CharacterTestBakedSocketTrack track) { track = null; return false; }
        }
        private sealed class EmptyBindingResolver : ICharacterTestWeaponTraceBindingResolver
        {
            public bool TryResolve(CharacterTestCharacterEntity entity, int equipment, int profile, out CharacterTestWeaponTraceBinding binding)
            { binding = default; return false; }
        }

        [Test]
        public void GuardLimitUsesActualActionClockAndWaitsForOwnedStopAcknowledgement()
        {
            var compilation = CompileGuard(2); var runtime = new GuardRuntime();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compilation.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit(); evaluator.Tick(2);
            Assert.AreEqual(1, runtime.Starts);
            runtime.ActualActionFrame = 10;
            for (int frame = 3; frame < 80; frame++) evaluator.Tick(frame);
            Assert.AreEqual(0, runtime.Stops, "World frames cannot consume a paused guard action clock.");
            runtime.ActualActionFrame = 11; evaluator.Tick(80); Assert.AreEqual(0, runtime.Stops);
            var snapshot = evaluator.CreateSnapshot();
            var restored = new CharacterTestBehaviorExecutionEvaluator(compilation.Program.Execution, runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));
            runtime.ActualActionFrame = 12; restored.Tick(81);
            Assert.AreEqual(1, runtime.Stops); Assert.AreEqual(101, runtime.ExpectedStop);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, restored.State);
            Assert.AreEqual(101, runtime.ActualActionInstanceId, "A queued stop has not removed the real action yet.");
            runtime.Commit(); restored.Tick(82); restored.Tick(83);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, restored.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, restored.EndReason);
            Assert.AreEqual(0, runtime.ActualActionInstanceId); Assert.AreEqual(1, runtime.Starts);
            Assert.AreEqual((int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed, restored.CreateSnapshot().Results.Single());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GuardLimitReportsFailedOrUnacknowledgedCleanup(bool reject)
        {
            var runtime = new GuardRuntime { RejectStop = reject };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(CompileGuard(2).Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit(); evaluator.Tick(2);
            runtime.ActualActionFrame = 12; evaluator.Tick(3);
            if (!reject) for (int frame = 4; frame <= 7; frame++) evaluator.Tick(frame);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Failed, evaluator.State);
            Assert.AreEqual(reject ? CharacterTestBehaviorExecutionEndReason.CleanupFailed : CharacterTestBehaviorExecutionEndReason.CleanupTimedOut,
                evaluator.EndReason);
        }

        [Test]
        public void GuardLimitIsASeparateHashedSourceParameterAndInvalidUsesAreRejected()
        {
            var shortGuard = CompileGuard(2); var longGuard = CompileGuard(18);
            Assert.AreNotEqual(shortGuard.Program.Execution.Identity, longGuard.Program.Execution.Identity);
            Assert.AreEqual(2, shortGuard.Program.Execution.Blocks[0].MaximumGuardFrames);
            Assert.AreEqual(10, shortGuard.Program.Execution.Blocks[0].GuardActiveStartFrame);
            Assert.AreEqual(129, shortGuard.Program.Execution.Blocks[0].GuardActiveEndFrame);
            var source = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition().Source;
            source.Timelines.Single().EventBlocks.Single().MaximumGuardFrames = -1;
            Assert.IsNotEmpty(CharacterTestTacticalAuthoringCompiler.ValidateShared(source));
            source.Timelines.Single().EventBlocks.Single().MaximumGuardFrames = 2;
            source.Slots.Single().Resolver = "lightAttackOpener";
            Assert.IsNotEmpty(CharacterTestTacticalAuthoringCompiler.ValidateShared(source));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition(0));
        }

        private static CharacterTestTacticalAuthoringCompilation CompileGuard(int limit)
        {
            var guard = LoadFormalGuard();
            var ctor = typeof(CharacterTestTacticalCardInputs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var inputs = (CharacterTestTacticalCardInputs)ctor.Invoke(new object[] { guard,
                new CharacterActionTacticalMetrics(guard, "guard-fixture-hash"), "guard-fixture", "guard-fixture-set", 1001,
                true, null, null, null, 0, null, CharacterTestTacticalInputRequirements.PrimaryAction });
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(
                CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition(limit).Source, inputs, out var compilation, out var issues),
                string.Join("; ", issues));
            return compilation;
        }

        [Test]
        public void ConfirmedGuardRuleOwnsTwoSecondsAndNaturalRecoveryBeforeCompletion()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition();
            Assert.AreEqual(2, definition.Revision);
            Assert.AreEqual(120, definition.Source.Timelines.Single().EventBlocks.Single().MaximumGuardFrames);
            var compiled = CompileGuard(120); var runtime = new GuardRuntime();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit(); evaluator.Tick(2);
            // World frames may advance while action time is frozen, including past the nominal watchdog.
            runtime.ActualActionFrame = 100;
            for (int world = 3; world < 500; world++) evaluator.Tick(world);
            Assert.AreEqual(0, runtime.Stops);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            for (int local = 129; local < 136; local++)
            {
                runtime.ActualActionFrame = local; evaluator.Tick(500 + local);
                Assert.AreEqual(0, runtime.Stops, "Natural Recovery must not be replaced by an orchestration cancel.");
                Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            }
            runtime.FinishNaturally(); evaluator.Tick(636); evaluator.Tick(637);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(1, runtime.Starts); Assert.AreEqual(0, runtime.Stops);
        }

        [Test]
        public void GuardPoseHoldsAtNormalSpeedEntryAndRecoveryReturnsToLocomotion()
        {
            var action = LoadFormalGuard();
            Assert.AreEqual(5, CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(action, 5));
            Assert.AreEqual(9, CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(action, 9));
            for (int local = 10; local <= 129; local++)
                Assert.AreEqual(10, CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(action, local));
            var active = new CharacterTestCharacterAnimationComponent(UnityEngine.Vector2.zero, 0, true, false,
                true, "art.character.skeleton.animation.katana_block", .08f, 2.266667f, 1, 288,
                "art.character.skeleton.animation.katana_block", 288, 288);
            Assert.IsTrue(CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardRecoveryPresentation(action, 129, active).ActionActive);
            var exit = CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardRecoveryPresentation(action, 130, active);
            Assert.IsFalse(exit.ActionActive); Assert.AreEqual(0, exit.RequestBindingId);
            Assert.AreEqual(string.Empty, exit.ActionKey);
            // An unrelated Guard has its original sampling and presentation semantics.
            var other = GuardAction(HitTargetStateFlags.Blocking, 3);
            Assert.AreEqual(20, CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(other, 20));
            Assert.IsTrue(CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardRecoveryPresentation(other, 10, active).ActionActive);
        }

        private sealed class GuardRuntime : ICharacterTestBehaviorExecutionRuntime, ICharacterTestBehaviorExecutionActionClock
        {
            internal int Starts, Stops; internal long ExpectedStop; internal bool RejectStop;
            public int EntityId => 1;
            public int ActualActionFrame { get; set; }
            public long ActualActionInstanceId { get; private set; }
            public long StartedActionInstanceId { get; private set; }
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState => ActualActionInstanceId > 0
                ? CharacterTestRuntimeAiLabLogicActionState.Running : CharacterTestRuntimeAiLabLogicActionState.Completed;
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState { get; private set; }
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate predicate) => true;
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject) => true;
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement movement, out CharacterTestRuntimeAiLabMovementTargetSample sample)
            { sample = default; throw new InvalidOperationException("Ordinary guard must not auto-move."); }
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn, out int x, out int z)
            { x = z = 0; throw new InvalidOperationException("Ordinary guard must not auto-turn."); }
            public bool TryStartAction(int id) { Starts++; ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending; return true; }
            public bool TryStopAction(long id) { Stops++; ExpectedStop = id;
                if (RejectStop) return false;
                ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending; return true; }
            internal void Commit()
            {
                if (Stops > 0) { if (ActualActionInstanceId == ExpectedStop) ActualActionInstanceId = 0; }
                else StartedActionInstanceId = ActualActionInstanceId = 101;
                ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
            }
            internal void FinishNaturally() { ActualActionInstanceId = 0; }
            public void CompleteActionCommand() { ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None; }
            public void ObserveOwnedAction(long id) { }
            public void SetExecutionControl(UnityEngine.Vector2 move, float speed, in CharacterTestBehaviorControlInput control)
            { Assert.AreEqual(UnityEngine.Vector2.zero, move); }
        }
        [Test]
        [Category("UnityNative")]
        public void BlockedHitPreservesHpButEmitsScatterPresentationAndBreaksEffectiveGuard()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var source = SpawnWitnessEntity(world); var target = SpawnWitnessEntity(world);
            var sourceId = new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value);
            var targetId = new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value);
            var hit = new HitResolveResult(sourceId, targetId, 459, 1, 1, new CombatFrame(1), HitResolveKind.Blocked, 12, 0, default);
            var payload = new MxFramework.Combat.Damage.DamageProfileResolveResult(success: true, reason: string.Empty,
                damageProfileId: 0, damageTypeId: 1, formulaId: MxFramework.Combat.Damage.DamageFormulaIds.Flat,
                incomingDamage: 12, staggerFrames: 0, impactForce: 0, knockback: default, impactPower: 10000,
                guardPressure: 0, presentationProfileId: 1, usage: default);
            var contributions = new HitTargetStateContributionStore(); contributions.Upsert(targetId, 1, 1, HitTargetStateFlags.Blocking);
            var lookup = new CharacterTestCombatEntityLookup(); lookup.Configure(world, contributions);
            int hp = world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp);
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(world, null, new RuntimeFrame(1), new[] { hit }, _ => payload);
            Assert.AreEqual(hp, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(1, applier.ScatterImpactResolvedEvents.Count);
            Assert.AreEqual(1, applier.HitPresentationEvents.Count);
            Assert.IsTrue(target.CombatState.IsCohesionBroken);
            Assert.AreEqual(1, applier.CohesionBreakEvents.Count);
            Assert.AreEqual(HitTargetStateFlags.None, lookup.ResolveTargetState(targetId) & HitTargetStateFlags.Blocking);
            Assert.AreEqual(1, contributions.Count, "Break revokes effective Blocking before owner cleanup without clearing another source.");
        }

        [Test]
        public void GuardDescriptionAndJsonReportTheIndependentLimit()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition(2);
            var block = definition.Source.Timelines.Single().EventBlocks.Single();
            Assert.IsTrue(CharacterTestTacticalBlockDescription.TryOrdinaryGuardText(definition, "guard-instance", block,
                definition.Source.Timelines.Single().Events.ToArray(), 1d / 60, null, null, out var text));
            StringAssert.Contains("0.033", text); StringAssert.Contains("散势", text);
            var json = CharacterTestTacticalAuthoringJson.Write(definition.Source);
            StringAssert.Contains("maximumGuardFrames", json);
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(
                json.Replace("\"maximumGuardFrames\": 2", "\"maximumGuardFrames\": 2.5")));
        }

        private static MxAnimationBakedWeaponTraceFrame StaticBakedFrame()
        {
            var root = new MxAnimationBakedVector3(0, 0, 0);
            var tip = new MxAnimationBakedVector3(0, 0, 1000000);
            return new MxAnimationBakedWeaponTraceFrame(1, 1, 1, root, tip, root, tip);
        }

        [Test]
        public void FormalGuardDispatchOwns120EffectiveHitFramesAndNaturalExit()
        {
            var runner = StartFormalGuard();
            var states = new HitTargetStateContributionStore();
            for (int frame = 1; frame <= 136; frame++)
            {
                var operation = runner.Tick();
                ApplyGuardLifecycle(operation.Events, states, CharacterActionTrackLifecycleBoundary.BeforeFrame);
                // Runtime combat samples after both lifecycle boundaries have been dispatched.
                ApplyGuardLifecycle(operation.Events, states, CharacterActionTrackLifecycleBoundary.AfterFrame);
                Assert.AreEqual(frame >= 10 && frame <= 129 ? HitTargetStateFlags.Blocking : HitTargetStateFlags.None,
                    states.ResolveFlags(Target), "authority hit sample at local frame " + frame);
            }
            Assert.AreEqual(CharacterActionInstanceState.Finished, runner.ActiveInstance.State);
            Assert.AreEqual(0, states.Count);
        }

        [Test]
        public void FormalGuardCancellationEmitsOwnedExitAndPreservesAnotherSource()
        {
            var runner = StartFormalGuard(); var states = new HitTargetStateContributionStore();
            for (int frame = 1; frame <= 10; frame++)
                ApplyGuardLifecycle(runner.Tick().Events, states, CharacterActionTrackLifecycleBoundary.BeforeFrame);
            states.Upsert(Target, 999, 999, HitTargetStateFlags.SuperArmor);
            var cancelled = runner.CancelActive();
            ApplyGuardLifecycle(cancelled.Events, states, CharacterActionTrackLifecycleBoundary.BeforeFrame);
            ApplyGuardLifecycle(cancelled.Events, states, CharacterActionTrackLifecycleBoundary.AfterFrame);
            Assert.AreEqual(HitTargetStateFlags.SuperArmor, states.ResolveFlags(Target));
            Assert.IsTrue(cancelled.Events.Any(e => e.Kind == CharacterActionRunnerEventKind.TrackLifecycle
                && e.TrackLifecycle.EventKind == CharacterActionTrackEventKind.SetHitTargetState
                && e.TrackLifecycle.ExitReason == CharacterActionTrackExitReason.ActionCancelled));
        }

        private static CharacterActionRunner StartFormalGuard()
        {
            var action = LoadFormalGuard();
            var plan = new CharacterActionPlan(action.ActionId.Value, action.ActionId, action.Category, action.Priority,
                action.DurationFrames.Value, action.Phases, CharacterActionTrackPlan.FromConfig(action),
                traceId: new RuntimeTraceIdentity(620001, 0, action.ActionId.Value, 0));
            var runner = new CharacterActionRunner();
            Assert.IsTrue(runner.Start(CharacterActionResolveResult.Success(plan), CharacterActionRunnerActionDefinition.FromConfig(action)).Accepted);
            return runner;
        }

        private static void ApplyGuardLifecycle(CharacterActionRunnerEvent[] events, HitTargetStateContributionStore states,
            CharacterActionTrackLifecycleBoundary boundary)
        {
            foreach (var item in events)
            {
                var lifecycle = item.TrackLifecycle;
                if (item.Kind != CharacterActionRunnerEventKind.TrackLifecycle
                    || lifecycle.EventKind != CharacterActionTrackEventKind.SetHitTargetState) continue;
                if (lifecycle.Phase == CharacterActionTrackLifecyclePhase.Exit)
                { if (lifecycle.Boundary == boundary) states.Remove(Target, lifecycle.ActionInstanceId, lifecycle.StableEventId); }
                else if (boundary == CharacterActionTrackLifecycleBoundary.BeforeFrame)
                    states.Upsert(Target, lifecycle.ActionInstanceId, lifecycle.StableEventId, lifecycle.Dispatch.TargetStateFlags);
            }
        }

        [Test]
        public void OrdinaryGuardRegistrationIsAnOwnerTransactionWithoutGivingCardsOrActions()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            var cards = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards);
            var groups = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups);
            var definition = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition();
            var json = CharacterTestTacticalCompositionJson.WriteShared(definition);
            var restored = CharacterTestTacticalCompositionJson.ReadShared(json);
            Assert.AreEqual(json, CharacterTestTacticalCompositionJson.WriteShared(restored));
            int revision = workspace.CurrentRevision;
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(restored, revision, out var issue), issue);
            Assert.AreEqual(revision + 1, workspace.CurrentRevision);
            Assert.IsTrue(workspace.CloneTacticalDefinitions().Any(d => d.DefinitionId == definition.DefinitionId
                && d.Revision == 2 && d.Source.Name == "普通防御"));
            Assert.AreEqual(cards, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards));
            Assert.AreEqual(groups, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups));
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(restored, workspace.CurrentRevision, out issue), issue);
            Assert.AreEqual(revision + 1, workspace.CurrentRevision);
            restored.Source.Name = "different content";
            Assert.IsFalse(workspace.TryAddReusableTacticalDefinition(restored, workspace.CurrentRevision, out issue));
            Assert.AreEqual(json, CharacterTestTacticalCompositionJson.WriteShared(
                workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == definition.DefinitionId)));
        }

        [Test]
        public void ProximalDirectionIncludesTheObservedExtensionMotionAndMatchesThePlanSample()
        {
            var frame = new WeaponTraceFrame(1,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(2)),
                FixVector3.Zero, new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                Fix64.Half, CombatPhysicsLayerMask.All, Fix64.One, Fix64.Half);
            Assert.IsTrue(WeaponTraceQueryBuilder.TryBuildCurrentProximalCapsule(frame, new CombatEntityId(1), 301, 0, 0, out var query));
            var hitPoint = new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.Half);
            var incoming = WeaponTraceQueryBuilder.ResolveProximalIncomingDisplacement(frame, hitPoint);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One), incoming);
            var sample = new CharacterTestDirectHitTracePlanSample(query, CharacterTestDirectHitTraceSampleKind.WeaponTraceProximal,
                1, 0, 0, 1, 301, 1, 1, -1, 1, frame);
            Assert.AreEqual(incoming, sample.ResolveIncomingDisplacement(hitPoint));
            Assert.AreEqual(FixVector3.Zero, WeaponTraceQueryBuilder.ResolveProximalIncomingDisplacement(default, hitPoint));
        }

        [Test]
        public void FormalGuardSourceImportsAndOwnsItsStateControlAndRealAnimationSpan()
        {
            var guard = LoadFormalGuard();
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(guard, out var issue), issue);
            var validation = CharacterActionValidationReport.Validate(new CharacterActionConfigRegistry(
                Array.Empty<CharacterActionSetConfig>(), new[] { guard }),
                new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Ignore));
            Assert.IsTrue(validation.Success, string.Join("; ", validation.Diagnostics.Select(d => d.ToString())));
            Assert.AreEqual(136, guard.DurationFrames);
            var blocking = guard.CombatTrack.Events.Single();
            Assert.AreEqual(HitTargetStateFlags.Blocking, blocking.TargetStateFlags);
            Assert.AreEqual(10, blocking.Frame); Assert.AreEqual(130, blocking.EndFrame);
            Assert.AreEqual(120, blocking.EndFrame - blocking.Frame);
            var motion = guard.MotionTrack.Events.Single();
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, motion.Kind);
            Assert.AreEqual(0f, motion.MovementControlScale);
            Assert.AreEqual(1f, motion.RotationControlScale);
            Assert.AreEqual(135, motion.EndFrame);
            var animation = guard.AnimationTrack.Events.Single();
            Assert.AreEqual("art.character.skeleton.animation.katana_block", animation.AnimationClipResourceKey);
            Assert.AreEqual(0, animation.Frame); Assert.AreEqual(135, animation.EndFrame);
            Assert.AreEqual(1f, animation.PlaybackSpeed);
            Assert.IsTrue(guard.InterruptRules.Any(r => r.SourceKind == CharacterActionSourceKind.CohesionBreak && r.Allow));
            Assert.IsTrue(guard.InterruptRules.Any(r => r.SourceKind == CharacterActionSourceKind.Death && r.Allow));
        }

        [Test]
        public void OrdinaryGuardCompilesFromTheFormalActionWithoutAnAttackOrTargetDependency()
        {
            var guard = LoadFormalGuard();
            var ctor = typeof(CharacterTestTacticalCardInputs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var inputs = (CharacterTestTacticalCardInputs)ctor.Invoke(new object[] { guard,
                new CharacterActionTacticalMetrics(guard, "guard-fixture-hash"), "guard-fixture", "guard-fixture-set", 1001,
                true, null, null, null, 0, null, CharacterTestTacticalInputRequirements.PrimaryAction });
            var source = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition().Source;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, inputs, out var compilation, out var issues),
                string.Join("; ", issues));
            Assert.IsNotNull(compilation.Program.Execution);
            Assert.AreEqual(CharacterTestTacticalInputRequirements.PrimaryAction, CharacterTestTacticalDependencies.Analyze(source));
        }

        private static CharacterActionConfig LoadFormalGuard()
        {
            var library = JObject.Parse(File.ReadAllText(Path.Combine("CharacterActionSource", "Libraries", "Common", "character_actions.json")));
            var action = library["actions"].OfType<JObject>().Single(a => a["tags"] is JArray tags && tags.Values<string>().Contains("ordinary-guard"));
            var actions = new JObject { ["schemaVersion"] = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                ["actions"] = new JArray(action.DeepClone()) };
            var sets = new JObject { ["schemaVersion"] = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ["actionSets"] = new JArray() };
            var imported = CharacterActionJsonImporter.Import(sets.ToString(), actions.ToString());
            Assert.IsTrue(imported.Success, string.Join("; ", imported.Diagnostics.Select(d => d.ToString())));
            return imported.Actions.Single();
        }

        [TestCase(0, 0, -1, true)]
        [TestCase(0, 0, 1, false)]
        [TestCase(0, 1, 0, true)]
        [TestCase(0, -1, 0, true)]
        [TestCase(90000, -1, 0, true)]
        [TestCase(90000, 1, 0, false)]
        [TestCase(180000, 0, 1, true)]
        [TestCase(270000, 1, 0, true)]
        [TestCase(0, 1, 1, false)]
        public void OrdinaryGuardUsesActualIncomingDirectionAndInclusiveFrontHalfSpace(int yaw, int x, int z, bool expected)
            => Assert.AreEqual(expected, CharacterTestCombatEntityLookup.IsWithinOrdinaryGuardArc(yaw,
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.FromInt(z))));

        [Test]
        public void UnknownOrVerticalMotionDoesNotInventAGuardableDirection()
        {
            Assert.IsFalse(CharacterTestCombatEntityLookup.IsWithinOrdinaryGuardArc(0, FixVector3.Zero));
            Assert.IsFalse(CharacterTestCombatEntityLookup.IsWithinOrdinaryGuardArc(0,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)));
        }

        [Test]
        public void CandidateStateResolverIsConsultedBeforeHitOnceConsumption()
        {
            var resolver = new DirectionalStateFixture();
            var results = new List<HitResolveResult>();
            var hitOnce = new HashSet<WeaponHitOnceKey>();
            var front = DirectionalCandidate(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One));
            new HitResolveSystem().Resolve(new[] { front }, hitOnce, results, resolver);
            Assert.AreEqual(HitResolveKind.Blocked, results.Single().Kind);
            Assert.AreEqual(1, resolver.CandidateReads);
            Assert.AreEqual(1, hitOnce.Count);
            results.Clear(); hitOnce.Clear();
            new HitResolveSystem().Resolve(new[] { DirectionalCandidate(new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)) }, hitOnce, results, resolver);
            Assert.AreEqual(HitResolveKind.Damage, results.Single().Kind);
            Assert.AreEqual(12, results.Single().Damage);
            results.Clear(); hitOnce.Clear();
            new HitResolveSystem().Resolve(new[] { DirectionalCandidate(FixVector3.Zero) }, hitOnce, results, resolver);
            Assert.AreEqual(HitResolveKind.Damage, results.Single().Kind);
        }

        [Test]
        public void BladeDirectionUsesTheHitMaterialPointRatherThanAlwaysTheTip()
        {
            var trace = new WeaponTraceFrame(1,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                new FixVector3(Fix64.One, Fix64.Zero, -Fix64.One),
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromRatio(1, 10), CombatPhysicsLayerMask.All);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One),
                WeaponTraceQueryBuilder.ResolveBladeIncomingDisplacement(trace, FixVector3.Zero));
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                WeaponTraceQueryBuilder.ResolveBladeIncomingDisplacement(trace, trace.TipNow));
        }

        private sealed class DirectionalStateFixture : IHitTargetStateCandidateResolver
        {
            internal int CandidateReads;
            public HitTargetStateFlags ResolveTargetState(CombatEntityId id) => HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking;
            public HitTargetStateFlags ResolveTargetState(HitCandidate candidate)
            {
                CandidateReads++;
                return HitTargetStateFlags.Alive | (CharacterTestCombatEntityLookup.IsWithinOrdinaryGuardArc(0, candidate.IncomingDirection)
                    ? HitTargetStateFlags.Blocking : HitTargetStateFlags.None);
            }
        }

        private static HitCandidate DirectionalCandidate(FixVector3 incoming)
        {
            var query = new CombatQueryHeader(1, CombatQueryKind.Capsule, new CombatEntityId(1), 1, 301, 0, CombatPhysicsLayerMask.All);
            var hit = new CombatQueryResult(query, Target, new CombatBodyId(2), new CombatColliderId(2),
                Fix64.Zero, FixVector3.Zero, new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            return new HitCandidate(new CombatEntityId(1), Target, 301, 1, 1, new CombatFrame(1),
                hit, 12, 4, FixVector3.Zero, HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking, incomingDirection: incoming);
        }

        [Test]
        public void OrdinaryGuardTemplateRoundTripsWithoutTargetOrAttackRangeDependencies()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition();
            Assert.AreEqual("tactical.shared.ordinary-guard", definition.DefinitionId);
            var source = definition.Source.Clone();
            Assert.AreEqual(4, source.Version);
            Assert.AreEqual("ordinaryGuard", source.Slots.Single().Resolver);
            Assert.IsNull(source.ActivationConditionRoot);
            Assert.AreEqual(CharacterTestTacticalInputRequirements.PrimaryAction, CharacterTestTacticalDependencies.Analyze(source));
            Assert.AreEqual(1, source.Timelines.Single().Events.Count);
            Assert.AreEqual("Action", source.Timelines.Single().EventBlocks.Single().Completion);
            Assert.IsNull(source.Timelines.Single().EventBlocks.Single().Timing, "Display duration must derive from the actual Guard action.");
            Assert.IsEmpty(CharacterTestTacticalAuthoringCompiler.ValidateShared(source));
        }

        [Test]
        public void FiniteGuardActionWithRealBlockingWindowQualifies()
        {
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(GuardAction(HitTargetStateFlags.Blocking), out string issue), issue);
        }

        [Test]
        public void GuardAnimationWithoutBlockingWindowDoesNotQualify()
        {
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(GuardAction(HitTargetStateFlags.None), out string issue));
            StringAssert.Contains("GuardBlockingWindowRequired", issue);
        }

        [Test]
        public void OrdinaryGuardDoesNotAcceptParryingOrInvincibleWindows()
        {
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(GuardAction(HitTargetStateFlags.Blocking | HitTargetStateFlags.Parrying), out string issue));
            StringAssert.Contains("OrdinaryGuardBlockingOnly", issue);
        }

        [Test]
        public void OrdinaryGuardQualificationDoesNotChangeOtherGuardActionContracts()
        {
            var otherGuard = GuardAction(HitTargetStateFlags.Parrying);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateAction(otherGuard, out string issue), issue);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(otherGuard, out issue));
            StringAssert.Contains("OrdinaryGuardBlockingOnly", issue);
        }

        [Test]
        public void BlockingWindowOutsideTheActivePhaseDoesNotQualify()
        {
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(GuardAction(HitTargetStateFlags.Blocking, 0), out string issue));
            StringAssert.Contains("GuardWindowOutsideActive", issue);
        }

        [TestCase(9, true)]
        [TestCase(10, false)]
        public void GuardExitBoundaryUsesTheFirstIneffectiveHitFrame(int exitFrame, bool accepted)
        {
            Assert.AreEqual(accepted, CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(GuardAction(HitTargetStateFlags.Blocking, end: exitFrame), out _));
        }

        private static CharacterActionConfig GuardAction(HitTargetStateFlags flags, int start = 3, int end = 8)
            => new CharacterActionConfig(new CharacterActionId(777777), "Guard contract fixture", CharacterActionCategory.Guard,
                CharacterActionTimelineAuthority.CharacterAuthored, Array.Empty<string>(), 0, 12,
                Array.Empty<CharacterActionRequirement>(), new[] {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 2),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 3, 8),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 9, 11) },
                Array.Empty<CharacterCancelRule>(), Array.Empty<CharacterInterruptRule>(),
                combatTrack: new CombatTrackConfig(0, flags == HitTargetStateFlags.None ? Array.Empty<CombatTrackEvent>() : new[] {
                    new CombatTrackEvent(start, CharacterActionTrackEventKind.SetHitTargetState, stableEventId: 201,
                        endFrame: end, targetStateFlags: flags, stableEventOrder: 1) }));

        [Test]
        public void StateEntryAndExitChangeTheRealHitResult()
        {
            var states = new HitTargetStateContributionStore();
            Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
            states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
            var blocked = Resolve(states);
            Assert.AreEqual(HitResolveKind.Blocked, blocked.Kind);
            Assert.IsFalse(blocked.IsAcceptedDamage);
            Assert.AreEqual(12, blocked.Damage, "Blocked keeps incoming damage evidence; it is not a zero-damage candidate.");
            Assert.IsTrue(states.Remove(Target, 10, 101));
            Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
            Assert.AreEqual(0, states.Count);
        }

        [Test]
        public void RemovingOneGuardOwnerDoesNotRemoveAnother()
        {
            var states = new HitTargetStateContributionStore();
            states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
            states.Upsert(Target, 11, 102, HitTargetStateFlags.Blocking);
            Assert.AreEqual(1, states.RemoveInstance(Target, 10));
            Assert.AreEqual(HitResolveKind.Blocked, Resolve(states).Kind);
            Assert.AreEqual(1, states.RemoveInstance(Target, 11));
            Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
        }

        [Test]
        public void InstanceCleanupPreservesOtherTransientStates()
        {
            var states = new HitTargetStateContributionStore();
            states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
            states.Upsert(Target, 11, 102, HitTargetStateFlags.SuperArmor);
            Assert.AreEqual(1, states.RemoveInstance(Target, 10));
            Assert.AreEqual(HitTargetStateFlags.SuperArmor, states.ResolveFlags(Target));
            Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
            Assert.AreEqual(0, Resolve(states).StaggerFrames);
        }

        [Test]
        public void RepeatedEntryAndCleanupDoNotLeakContributions()
        {
            var states = new HitTargetStateContributionStore();
            for (int cycle = 0; cycle < 8; cycle++)
            {
                states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
                states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
                Assert.AreEqual(1, states.Count);
                Assert.AreEqual(1, states.RemoveInstance(Target, 10));
                Assert.AreEqual(0, states.RemoveInstance(Target, 10));
                Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
            }
        }

        [Test]
        public void EntityCleanupDoesNotClearOtherEntities()
        {
            var states = new HitTargetStateContributionStore();
            var other = new CombatEntityId(3);
            states.Upsert(Target, 10, 101, HitTargetStateFlags.Blocking);
            states.Upsert(other, 11, 102, HitTargetStateFlags.Blocking);
            Assert.AreEqual(1, states.ClearEntity(Target));
            Assert.AreEqual(HitResolveKind.Damage, Resolve(states).Kind);
            Assert.AreEqual(HitTargetStateFlags.Blocking, states.ResolveFlags(other));
            states.Clear();
            Assert.AreEqual(0, states.Count);
            Assert.AreEqual(HitTargetStateFlags.None, states.ResolveFlags(other));
        }

        private static HitResolveResult Resolve(HitTargetStateContributionStore states)
        {
            var query = new CombatQueryHeader(1, CombatQueryKind.Capsule, new CombatEntityId(1), 1, 301, 0, CombatPhysicsLayerMask.All);
            var hit = new CombatQueryResult(query, Target, new CombatBodyId(2), new CombatColliderId(2),
                Fix64.Zero, FixVector3.Zero, FixVector3.Zero);
            var candidate = new HitCandidate(new CombatEntityId(1), Target, 301, 1, 1, new CombatFrame(1),
                hit, 12, 4, FixVector3.Zero, HitTargetStateFlags.Alive | states.ResolveFlags(Target));
            var results = new List<HitResolveResult>();
            new HitResolveSystem().Resolve(new[] { candidate }, new HashSet<WeaponHitOnceKey>(), results);
            return results[0];
        }
    }
}
