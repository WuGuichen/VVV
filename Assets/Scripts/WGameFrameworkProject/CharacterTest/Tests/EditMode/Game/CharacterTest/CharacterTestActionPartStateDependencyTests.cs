using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestActionPartStateDependencyTests
    {
        private const int TailPartStateId = CharacterTestAuthoredContentIds.D400_Id43;
        private static readonly CharacterActionId Attack6ActionId = new CharacterActionId(940002);
        private const int BiteEventId = CharacterTestAuthoredContentIds.D401_Id146;
        private const int TailSweepEventId = CharacterTestAuthoredContentIds.D401_Id147;
        private static readonly CharacterActionId Attack30ActionId = new CharacterActionId(940004);
        private const int TailSpinEventId = CharacterTestAuthoredContentIds.D401_Id150;
        private const string SourceActionsPath =
            "Authoring/CharacterPackages/character-000003/config/character_actions.json";
        private const string GeneratedActionsPath =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/000003/character_actions.json";
        private const string GeneratedActionSetPath =
            "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/000003/character_action_sets.json";

        [Test]
        public void Dragon14SourceAndProjection_ImportExactTailDependenciesAndValidate()
        {
            JObject source = JObject.Parse(File.ReadAllText(SourceActionsPath));
            JObject generated = JObject.Parse(File.ReadAllText(GeneratedActionsPath));

            AssertJsonDependency(source, 940002, TailSweepEventId);
            AssertJsonDependency(source, 940004, TailSpinEventId);
            Assert.IsTrue(JToken.DeepEquals(
                FindJsonEvent(source, 940002, TailSweepEventId),
                FindJsonEvent(generated, 940002, TailSweepEventId)));
            Assert.IsTrue(JToken.DeepEquals(
                FindJsonEvent(source, 940004, TailSpinEventId),
                FindJsonEvent(generated, 940004, TailSpinEventId)));

            CharacterActionJsonImportResult import =
                CharacterActionJsonImporter.ImportActions(source.ToString(Formatting.None));
            Assert.IsTrue(import.Success, FirstImportDiagnostic(import));
            AssertImportedDependency(import, 940002, TailSweepEventId);
            AssertImportedDependency(import, 940004, TailSpinEventId);
        }

        [TestCase("missing-id", "", 3)]
        [TestCase("missing-mask", TailPartStateId, 0)]
        [TestCase("future-mask", TailPartStateId, 8)]
        public void PartStateDependency_InvalidOrFutureRawValue_IsPreservedAndFailsValidation(
            string mutation,
            string expectedPartStateId,
            int expectedRawMask)
        {
            JObject source = JObject.Parse(File.ReadAllText(SourceActionsPath));
            JObject tailSpin = FindJsonEvent(source, 940004, TailSpinEventId);
            if (mutation == "missing-id")
                tailSpin.Property("requiredPartStateId")?.Remove();
            else if (mutation == "missing-mask")
                tailSpin.Property("allowedPartStructureStateMask")?.Remove();
            else
                tailSpin["allowedPartStructureStateMask"] = expectedRawMask;

            CharacterActionJsonImportResult import =
                CharacterActionJsonImporter.ImportActions(source.ToString(Formatting.None));
            Assert.IsTrue(import.Success, FirstImportDiagnostic(import));
            CharacterActionConfig action = FindAction(import, 940004);
            CombatTrackEvent trackEvent = FindEvent(action, TailSpinEventId);
            Assert.AreEqual(expectedPartStateId, trackEvent.RequiredPartStateId);
            Assert.AreEqual(expectedRawMask, (int)trackEvent.AllowedPartStructureStateMask);

            CharacterActionDiagnostic[] diagnostics =
                CharacterActionValidation.ValidateActionConfig(action);
            Assert.IsTrue(Array.Exists(
                diagnostics,
                diagnostic => diagnostic.Code
                    == CharacterActionDiagnosticCodes.InvalidPartStateDependency));
        }

        [Test]
        public void Dragon14SeveredTail_KeepsAttack6BiteButRejectsTailOnlyAttack30()
        {
            CharacterTestCharacterActionRuntime actionRuntime = LoadFormalActionRuntime();
            RuntimeFixture fixture = CreateRuntimeFixture(actionRuntime);
            CharacterActionConfig attack6 = FindAction(actionRuntime, 940002);
            CharacterActionConfig attack30 = FindAction(actionRuntime, 940004);

            Assert.IsTrue(actionRuntime.EvaluatePartStateEligibility(fixture.Source, attack6).IsEligible);
            Assert.IsTrue(actionRuntime.EvaluatePartStateEligibility(
                fixture.Source,
                Attack6ActionId,
                TailSweepEventId).IsEligible);
            SeverTail(fixture.Source, breakFrame: 10, severFrame: 11);

            Assert.IsTrue(
                actionRuntime.EvaluatePartStateEligibility(fixture.Source, attack6).IsEligible,
                "Attack 940002 remains startable because its bite event is independent.");
            Assert.AreEqual(
                CharacterTestActionPartStateEligibilityStatus.Eligible,
                actionRuntime.EvaluatePartStateEligibility(
                    fixture.Source,
                    Attack6ActionId,
                    BiteEventId).Status);
            Assert.AreEqual(
                CharacterTestActionPartStateEligibilityStatus.PartStateSevered,
                actionRuntime.EvaluatePartStateEligibility(
                    fixture.Source,
                    Attack6ActionId,
                    TailSweepEventId).Status);
            Assert.AreEqual(
                CharacterTestActionPartStateEligibilityStatus.PartStateSevered,
                actionRuntime.EvaluatePartStateEligibility(fixture.Source, attack30).Status);

            Assert.IsFalse(actionRuntime.TryStartIntent(
                fixture.Source,
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id61,
                new RuntimeFrame(12)));
            Assert.AreEqual(
                CharacterActionDiagnosticCodes.PartStateDependencySevered,
                fixture.Source.Action.LastRejectReason);
        }

        [Test]
        public void ActiveTailTrace_SeveredPrunesBeforeNextFrameWithoutUndoingAcceptedHit()
        {
            CharacterTestCharacterActionRuntime actionRuntime = LoadFormalActionRuntime();
            RuntimeFixture fixture = CreateRuntimeFixture(actionRuntime);
            CharacterActionConfig attack6 = FindAction(actionRuntime, 940002);
            CombatTrackEvent tailSweep = FindEvent(attack6, TailSweepEventId);
            var windows = new CharacterTestHitTraceWindowState();
            RuntimeFrame acceptedFrame = new RuntimeFrame(20);

            fixture.Source.EnqueueCombatRequest(CreateEnterRequest(
                fixture.Source,
                attack6,
                tailSweep,
                acceptedFrame));
            windows.ProcessStartCombatRequests(fixture.World, acceptedFrame, null);
            fixture.Source.ClearCombatRequests();
            Assert.AreEqual(1, windows.ActiveTraceCount);

            fixture.Target.SetCombatState(fixture.Target.CombatState.ApplyHit(
                damage: 7,
                staggerFrames: 0,
                frame: acceptedFrame.Value,
                sourceEntityId: fixture.Source.Id.Value,
                traceId: 7001));
            int acceptedHealth = fixture.Target.CombatState.Health;
            SeverTail(fixture.Source, breakFrame: 21, severFrame: 22);

            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(fixture.World);
            windows.PruneIneligiblePartDependentTraces(
                lookup,
                actionRuntime,
                new RuntimeFrame(23),
                null);

            Assert.AreEqual(0, windows.ActiveTraceCount);
            Assert.AreEqual(
                acceptedHealth,
                fixture.Target.CombatState.Health,
                "The next-frame gate cannot retroactively undo an accepted hit.");
        }

        [Test]
        public void PlannerOrdinaryAndJoint_PreserveMissingBakeReasonAndUseTypedPartStateReason()
        {
            using (PlannerFixture fixture = CreatePlannerFixtureWithMissingSourceBake())
            {
                Assert.IsFalse(fixture.Adapter.TryPreviewPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    Attack30ActionId,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult missingBake));
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable,
                    missingBake.FailureReason);
                Assert.IsFalse(fixture.Adapter.TryPrepareJointPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    Attack30ActionId,
                    fixture.PlannerFrame,
                    out _,
                    out CharacterTestAttackOpportunityFailureReason jointMissingBake));
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable,
                    jointMissingBake);

                SeverTail(fixture.Source, breakFrame: 30, severFrame: 31);

                Assert.IsFalse(fixture.Adapter.TryPreviewPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    Attack30ActionId,
                    new RuntimeFrame(32),
                    out CharacterTestAttackOpportunityResult ordinaryRejected));
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected,
                    ordinaryRejected.FailureReason);
                Assert.IsFalse(fixture.Adapter.TryPrepareJointPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    Attack30ActionId,
                    new RuntimeFrame(32),
                    out _,
                    out CharacterTestAttackOpportunityFailureReason jointRejected));
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected,
                    jointRejected);
            }
        }

        [Test]
        public void EligibilityAndTracePrune_WarmedHotPathAllocatesZeroBytes()
        {
            CharacterTestCharacterActionRuntime actionRuntime = LoadFormalActionRuntime();
            RuntimeFixture fixture = CreateRuntimeFixture(actionRuntime);
            CharacterActionConfig attack30 = FindAction(actionRuntime, 940004);
            CombatTrackEvent tailSpin = FindEvent(attack30, TailSpinEventId);
            var windows = new CharacterTestHitTraceWindowState();
            RuntimeFrame frame = new RuntimeFrame(40);
            fixture.Source.EnqueueCombatRequest(CreateEnterRequest(
                fixture.Source,
                attack30,
                tailSpin,
                frame));
            windows.ProcessStartCombatRequests(fixture.World, frame, null);
            fixture.Source.ClearCombatRequests();
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(fixture.World);

            CharacterTestActionPartStateEligibility last = default;
            for (int i = 0; i < 64; i++)
            {
                last = actionRuntime.EvaluatePartStateEligibility(fixture.Source, attack30);
                windows.PruneIneligiblePartDependentTraces(lookup, actionRuntime, frame, null);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
            {
                last = actionRuntime.EvaluatePartStateEligibility(fixture.Source, attack30);
                windows.PruneIneligiblePartDependentTraces(lookup, actionRuntime, frame, null);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(last.IsEligible);
            Assert.AreEqual(1, windows.ActiveTraceCount);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestCharacterActionRuntime LoadFormalActionRuntime()
        {
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime.ImportResult));
            Assert.IsTrue(runtime.Registry.TryGetAction(Attack6ActionId, out _));
            Assert.IsTrue(runtime.Registry.TryGetAction(Attack30ActionId, out _));
            return runtime;
        }

        private static RuntimeFixture CreateRuntimeFixture(
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            CharacterTestCharacterDefinition definition = FindDragonDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity source = Spawn(world, definition, teamId: 1, x: 0f);
            CharacterTestCharacterEntity target = Spawn(world, definition, teamId: 2, x: 1f);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                source.BodyPartState.InitializationStatus);
            return new RuntimeFixture(world, source, target, actionRuntime);
        }

        private static PlannerFixture CreatePlannerFixtureWithMissingSourceBake()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-lcc-m3-t4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            CharacterTestCharacterRuntimeModule runtime = null;
            try
            {
                JObject actionRoot = JObject.Parse(File.ReadAllText(SourceActionsPath));
                JObject plannerAction = (JObject)FindJsonAction(actionRoot, 940004).DeepClone();
                plannerAction["category"] = CharacterActionCategory.BasicAttack.ToString();
                actionRoot["actions"] = new JArray(plannerAction);
                File.WriteAllText(
                    Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionsFileName),
                    actionRoot.ToString(Formatting.Indented));

                JObject setRoot = JObject.Parse(File.ReadAllText(GeneratedActionSetPath));
                JObject actionSet = (JObject)((JArray)setRoot["actionSets"])[0];
                actionSet["reactionProfileId"] = string.Empty;
                actionSet["commandBindings"] = new JArray();
                actionSet["commandSequences"] = new JArray(new JObject
                {
                    ["intentId"] = "LightAttack",
                    ["actionIds"] = new JArray(Attack30ActionId.Value),
                    ["wrapAfterLast"] = true,
                    ["priority"] = 20,
                    ["allowQueue"] = false,
                    ["queueWindowFrames"] = 0
                });
                File.WriteAllText(
                    Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionSetsFileName),
                    setRoot.ToString(Formatting.Indented));

                var physics = new CharacterTestCharacterPhysicsRuntime(
                    new CombatPhysicsWorld(),
                    CombatPhysicsLayerMask.FromLayer(1),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                    Fix64.FromRatio(1, 100),
                    Fix64.Zero,
                    staticColliderCount: 1,
                    sourceDescription: "lcc-m3-t4.planner-part-state");
                runtime = new CharacterTestCharacterRuntimeModule(
                    null,
                    NullRuntimeLogger.Instance,
                    actionConfigOptions: new CharacterTestCharacterActionConfigSourceOptions(
                        new[] { directory }),
                    physicsRuntime: physics,
                    createViews: false);

                CharacterTestCharacterDefinition dragon = FindDragonDefinition();
                var missingBakeDefinition = new CharacterTestCharacterDefinition(
                    dragon.CharacterId + 100000,
                    dragon.DisplayName,
                    default(ResourceKey),
                    dragon.DefaultTeamId,
                    dragon.AnimationBinding,
                    dragon.PhysicsProfile,
                    "Assets/MxFrameworkGenerated/CharacterPackages/lcc-m3-t4-missing-bake");
                CharacterTestCharacterEntity source = Spawn(
                    runtime.World,
                    missingBakeDefinition,
                    teamId: 1,
                    x: 0f);
                CharacterTestCharacterEntity target = Spawn(
                    runtime.World,
                    dragon,
                    teamId: 2,
                    x: 1f);
                RuntimeFrame completedFrame = new RuntimeFrame(1);
                runtime.Tick(new RuntimeTickContext(
                    completedFrame.Value,
                    1d / 60d,
                    completedFrame.Value / 60d,
                    RuntimeTickStage.Simulation));

                CharacterImportedPackage package =
                    CharacterTestGeneratedPackageSource.LoadPackage(dragon.GeneratedPackageRoot);
                Assert.IsNotNull(package);
                Assert.AreEqual(
                    CharacterTestBodyPartStateInitializationStatus.Ready,
                    source.ReinitializeBodyPartState(package.Geometry));
                Assert.IsTrue(
                    runtime.TryGetDamageGeometrySnapshot(target.Id.Value, out _),
                    "Planner fixture target geometry was not produced by the completed frame.");
                Assert.IsTrue(
                    runtime.ActionRuntime.Registry.TryGetAction(Attack30ActionId, out _),
                    "Planner fixture action import failed: "
                    + FirstImportDiagnostic(runtime.ActionRuntime.ImportResult));

                return new PlannerFixture(
                    directory,
                    runtime,
                    source,
                    target,
                    new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(runtime),
                    new RuntimeFrame(2));
            }
            catch
            {
                runtime?.Dispose();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
                throw;
            }
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterDefinition definition,
            int teamId,
            float x)
        {
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return entity;
        }

        private static CharacterActionCombatRequest CreateEnterRequest(
            CharacterTestCharacterEntity source,
            CharacterActionConfig action,
            CombatTrackEvent trackEvent,
            RuntimeFrame frame)
        {
            return new CharacterActionCombatRequest(
                new CharacterActionAdapterDispatchMetadata(
                    frame,
                    instanceId: 1001L,
                    planId: 2001L,
                    actionId: action.ActionId,
                    localFrame: trackEvent.Frame,
                    stableEventId: trackEvent.StableEventId,
                    sourceId: source.Id.Value,
                    traceId: new RuntimeTraceIdentity(trackEvent.StableEventId, source.Id.Value, 1001L),
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter),
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value),
                trackEvent.Kind,
                trackEvent.CombatActionId,
                trackEvent.TraceProfileId,
                trackEvent.EquipmentSlotId,
                trackEvent.DamagePayload,
                trackEvent.HitEvent,
                trackEvent.HitVolume);
        }

        private static CharacterTestCharacterDefinition FindDragonDefinition()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
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

        private static void SeverTail(
            CharacterTestCharacterEntity entity,
            long breakFrame,
            long severFrame)
        {
            CharacterTestBodyPartStateComponent state = entity.BodyPartState;
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(TailPartStateId, out int index));
            Assert.IsTrue(state.TryGetDefinition(index, out CharacterTestBodyPartStateDefinition definition));
            Assert.IsTrue(state.TryBeginFrame(breakFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    breakFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex: index,
                    finalDamage: int.MaxValue)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    severFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    definitionIndex: index,
                    severTag: definition.SeverRequiredTag,
                    correlationId: severFrame)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
        }

        private static void AssertJsonDependency(JObject root, int actionId, int eventId)
        {
            JObject trackEvent = FindJsonEvent(root, actionId, eventId);
            Assert.AreEqual(TailPartStateId, (string)trackEvent["requiredPartStateId"]);
            Assert.AreEqual(3, (int)trackEvent["allowedPartStructureStateMask"]);
        }

        private static void AssertImportedDependency(
            CharacterActionJsonImportResult import,
            int actionId,
            int eventId)
        {
            CharacterActionConfig action = FindAction(import, actionId);
            CombatTrackEvent trackEvent = FindEvent(action, eventId);
            Assert.AreEqual(TailPartStateId, trackEvent.RequiredPartStateId);
            Assert.AreEqual(
                CharacterActionPartStructureStateMask.Intact
                | CharacterActionPartStructureStateMask.Broken,
                trackEvent.AllowedPartStructureStateMask);
            CharacterActionDiagnostic[] diagnostics =
                CharacterActionValidation.ValidateActionConfig(action);
            Assert.IsFalse(Array.Exists(
                diagnostics,
                diagnostic => diagnostic.Code
                    == CharacterActionDiagnosticCodes.InvalidPartStateDependency));
        }

        private static JObject FindJsonAction(JObject root, int actionId)
        {
            foreach (JToken item in (JArray)root["actions"])
            {
                if ((int)item["id"] == actionId)
                    return (JObject)item;
            }
            Assert.Fail("Missing action " + actionId + ".");
            return null;
        }

        private static JObject FindJsonEvent(JObject root, int actionId, int eventId)
        {
            JObject action = FindJsonAction(root, actionId);
            foreach (JToken item in (JArray)action["combatTrack"]["events"])
            {
                if ((int)item["stableEventId"] == eventId)
                    return (JObject)item;
            }
            Assert.Fail("Missing event " + eventId + ".");
            return null;
        }

        private static CharacterActionConfig FindAction(
            CharacterActionJsonImportResult import,
            int actionId)
        {
            for (int i = 0; i < import.Actions.Length; i++)
            {
                if (import.Actions[i].ActionId.Value == actionId)
                    return import.Actions[i];
            }
            Assert.Fail("Missing imported action " + actionId + ".");
            return null;
        }

        private static CharacterActionConfig FindAction(
            CharacterTestCharacterActionRuntime runtime,
            int actionId)
        {
            Assert.IsTrue(runtime.Registry.TryGetAction(
                new CharacterActionId(actionId),
                out CharacterActionConfig action));
            return action;
        }

        private static CombatTrackEvent FindEvent(CharacterActionConfig action, int eventId)
        {
            CombatTrackEvent[] events = action.CombatTrack?.Events;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                if (events[i].StableEventId == eventId)
                    return events[i];
            }
            Assert.Fail("Missing imported event " + eventId + ".");
            return default;
        }

        private static string FirstImportDiagnostic(CharacterActionJsonImportResult import)
        {
            return import != null && import.Diagnostics.Length > 0
                ? import.Diagnostics[0].ToString()
                : string.Empty;
        }

        private sealed class RuntimeFixture
        {
            internal RuntimeFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                CharacterTestCharacterActionRuntime actionRuntime)
            {
                World = world;
                Source = source;
                Target = target;
                ActionRuntime = actionRuntime;
            }

            internal CharacterTestCharacterRuntimeWorld World { get; }
            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterTestCharacterActionRuntime ActionRuntime { get; }
        }

        private sealed class PlannerFixture : IDisposable
        {
            internal PlannerFixture(
                string directory,
                CharacterTestCharacterRuntimeModule runtime,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter,
                RuntimeFrame plannerFrame)
            {
                DirectoryPath = directory;
                Runtime = runtime;
                Source = source;
                Target = target;
                Adapter = adapter;
                PlannerFrame = plannerFrame;
            }

            internal string DirectoryPath { get; }
            internal CharacterTestCharacterRuntimeModule Runtime { get; }
            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterTestRuntimeAiPlannerAttackOpportunityAdapter Adapter { get; }
            internal RuntimeFrame PlannerFrame { get; }

            public void Dispose()
            {
                Runtime.Dispose();
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
