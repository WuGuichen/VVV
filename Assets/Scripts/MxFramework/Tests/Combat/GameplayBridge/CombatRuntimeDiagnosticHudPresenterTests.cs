using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Demo.CombatAnimation;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatRuntimeDiagnosticHudPresenterTests
    {
        private const int HpAttributeId = 100;

        [Test]
        public void Build_EmptySourcesFormatsEveryDiagnosticRegion()
        {
            var presenter = new CombatRuntimeDiagnosticHudPresenter();

            CombatRuntimeDiagnosticHudModel model = presenter.Build(
                new RuntimeFrame(3),
                new GameplayComponentWorld(),
                new CombatEntityGameplayMap(),
                Array.Empty<RuntimeCommand>(),
                Array.Empty<HitResolveResult>(),
                Array.Empty<IRuntimeHashContributor>());

            Assert.That(model.ActionStateRows[0], Does.Contain("No combat action"));
            Assert.That(model.HitApplicationRows[0], Does.Contain("No hit application"));
            Assert.That(model.GameplayAttributeRows[0], Does.Contain("No gameplay attribute"));
            Assert.That(model.BridgeMapRows[0], Does.Contain("No combat/gameplay"));
            Assert.That(model.RuntimeHashRows[0], Does.Contain("unavailable"));
            Assert.That(model.EventQueueRows[0], Does.Contain("Pending=0"));
        }

        [Test]
        public void Build_NormalSourcesFormatsReadOnlyRuntimeState()
        {
            var presenter = new CombatRuntimeDiagnosticHudPresenter();
            var world = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayEntityId player = world.CreateEntity();
            GameplayEntityId dummy = world.CreateEntity();
            world.GetOrCreateStore<GameplayIdentityComponent>().Set(player, new GameplayIdentityComponent(10));
            world.GetOrCreateStore<GameplayIdentityComponent>().Set(dummy, new GameplayIdentityComponent(20));
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                dummy,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(HpAttributeId, 100, 85)));
            world.GetOrCreateStore<CombatActionStateComponent>().Set(
                player,
                CombatActionStateComponent.Active(new CombatActionState(
                    new CombatEntityId(1),
                    actionId: 1001,
                    localFrame: 12,
                    startedAtFrame: new CombatFrame(5),
                    phase: CombatActionPhase.Active)));

            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), player);
            map.Register(new CombatEntityId(2), dummy);
            var outputCommands = new List<RuntimeCommand>
            {
                GameplayRuntimeCommandFactory.AddComponentAttribute(
                    new RuntimeFrame(9),
                    dummy,
                    HpAttributeId,
                    -15,
                    sourceId: 0,
                    traceId: "201"),
            };
            world.Events.Enqueue(new RuntimeFrame(10), new GameplayRuntimeEvent(
                new RuntimeFrame(10),
                GameplayRuntimeEventType.ComponentAttributeChanged,
                GameplayRuntimeCommandIds.AddComponentAttribute,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: "201",
                componentEntityIndex: dummy.Index,
                componentEntityGeneration: dummy.Generation,
                attributeId: HpAttributeId,
                oldAttributeValue: 100,
                newAttributeValue: 85,
                attributeDelta: -15));

            CombatRuntimeDiagnosticHudModel model = presenter.Build(
                new RuntimeFrame(10),
                world,
                map,
                outputCommands,
                Array.Empty<HitResolveResult>(),
                new[] { new GameplayComponentWorldHashContributor(world) });

            Assert.That(model.ActionStateRows[0], Does.Contain("action=1001"));
            Assert.That(model.ActionStateRows[0], Does.Contain("localFrame=12"));
            Assert.That(model.HitApplicationRows[0], Does.Contain("delta=-15"));
            Assert.That(model.GameplayAttributeRows[0], Does.Contain("attribute=100"));
            Assert.That(model.GameplayAttributeRows[0], Does.Contain("current=85"));
            Assert.That(model.BridgeMapRows[0], Does.Contain("Combat 1"));
            Assert.That(model.RuntimeHashRows[0], Does.Contain("Demo diagnostic hash frame=10"));
            Assert.That(model.RuntimeHashRows[1], Does.Contain(GameplayComponentWorldHashContributor.StableContributorId));
            Assert.That(model.EventQueueRows[0], Does.Contain("Pending=1"));
            Assert.That(model.EventQueueRows[0], Does.Contain("frames=10-10"));
            Assert.AreEqual(1, world.Events.PendingCount, "Diagnostic HUD must not drain RuntimeEventQueue.");
        }

        [Test]
        public void Build_HitApplicationFallsBackToReadOnlyHitResultsWhenCommandsAreEmpty()
        {
            var presenter = new CombatRuntimeDiagnosticHudPresenter();

            CombatRuntimeDiagnosticHudModel model = presenter.Build(
                new RuntimeFrame(7),
                new GameplayComponentWorld(),
                new CombatEntityGameplayMap(),
                Array.Empty<RuntimeCommand>(),
                new[]
                {
                    new HitResolveResult(
                        new CombatEntityId(1),
                        new CombatEntityId(2),
                        1001,
                        3,
                        201,
                        new CombatFrame(7),
                        HitResolveKind.Damage,
                        15,
                        0,
                        FixVector3.Zero),
                },
                Array.Empty<IRuntimeHashContributor>());

            Assert.That(model.HitApplicationRows[0], Does.Contain("Result Damage"));
            Assert.That(model.HitApplicationRows[0], Does.Contain("damage=15"));
        }
    }
}
