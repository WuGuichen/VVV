using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestHitTargetStateEventTests
    {
        [Test]
        public void SetHitTargetState_EnterActiveExitDrivesUnifiedResolver()
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
                new CharacterTestCharacterDefinition(
                    101,
                    "State Target",
                    default(ResourceKey),
                    1,
                    default));
            var state = new CharacterTestHitTraceWindowState();
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(world, state.TargetStateContributions);
            var combatEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);

            entity.EnqueueCombatRequest(CreateRequest(combatEntityId, CharacterActionTrackLifecyclePhase.Enter));
            state.ProcessStartCombatRequests(world, RuntimeFrame.Zero, null);
            Assert.AreEqual(
                HitTargetStateFlags.Alive | HitTargetStateFlags.Unhittable,
                lookup.ResolveTargetState(combatEntityId));

            entity.ClearCombatRequests();
            entity.EnqueueCombatRequest(CreateRequest(combatEntityId, CharacterActionTrackLifecyclePhase.Active));
            state.ProcessStartCombatRequests(world, new RuntimeFrame(1), null);
            Assert.AreEqual(1, state.TargetStateContributions.Count);

            entity.ClearCombatRequests();
            entity.EnqueueCombatRequest(CreateRequest(
                combatEntityId,
                CharacterActionTrackLifecyclePhase.Exit,
                CharacterActionTrackLifecycleBoundary.AfterFrame));
            state.ProcessStopCombatRequestsAndClear(world, new RuntimeFrame(2), null);
            Assert.AreEqual(HitTargetStateFlags.Alive, lookup.ResolveTargetState(combatEntityId));
        }

        private static CharacterActionCombatRequest CreateRequest(
            CombatEntityId entityId,
            CharacterActionTrackLifecyclePhase phase,
            CharacterActionTrackLifecycleBoundary boundary = CharacterActionTrackLifecycleBoundary.BeforeFrame)
        {
            return new CharacterActionCombatRequest(
                new CharacterActionAdapterDispatchMetadata(
                    RuntimeFrame.Zero,
                    instanceId: 77,
                    planId: 1,
                    actionId: new CharacterActionId(950015),
                    localFrame: 8,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0024,
                    sourceId: 1,
                    traceId: new RuntimeTraceIdentity(997818),
                    lifecyclePhase: phase,
                    lifecycleBoundary: boundary,
                    lifecycleExitReason: phase == CharacterActionTrackLifecyclePhase.Exit
                        ? CharacterActionTrackExitReason.NaturalEnd
                        : CharacterActionTrackExitReason.None),
                entityId,
                CharacterActionTrackEventKind.SetHitTargetState,
                HitTargetStateFlags.Unhittable);
        }
    }
}
