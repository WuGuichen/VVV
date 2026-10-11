using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerCombatSensorBridgeTests
    {
        private const int Layer = 3;
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(7601);

        [Test]
        public void TriggerReadSeam_ReportsCatalogAndEnabledStateWithoutThrowing()
        {
            TestComposition composition = CreateComposition();

            Assert.That(
                composition.Runtime.GameplayTriggerCatalogHash,
                Is.EqualTo(composition.Plan.GameplayTriggerCatalogHash));
            Assert.That(
                composition.Runtime.TryGetSourceEnabled(SourceBinding(1), out bool enabled),
                Is.True);
            Assert.That(enabled, Is.True);
            Assert.That(
                composition.Runtime.TryGetSourceEnabled(default, out bool unknownEnabled),
                Is.False);
            Assert.That(unknownEnabled, Is.False);

            Assert.That(
                composition.Runtime.TrySetEnabled(
                    composition.Runtime.GetInstanceId(0),
                    false,
                    out GameplayTriggerResult setResult),
                Is.True,
                setResult.ErrorCode.ToString());
            Assert.That(
                composition.Runtime.TryGetSourceEnabled(SourceBinding(1), out enabled),
                Is.True);
            Assert.That(enabled, Is.False);
        }

        [Test]
        public void Catalog_PermutationProviderReuseAndPresenceEpoch_AreCanonical()
        {
            TestComposition composition = CreateComposition(
                bindingCount: 2,
                capabilities: RequiredCapabilities | GameplayTriggerSourceEntityCapability.PresenceEpoch,
                providerId: 44);
            GameplayTriggerCombatSensorDescriptor first = Descriptor(1, 44, SphereQuery(10), 8, 4);
            GameplayTriggerCombatSensorDescriptor second = Descriptor(2, 44, AabbQuery(20), 8, 4);
            var reversedInput = new[] { second, first };

            Assert.That(
                GameplayTriggerCombatSensorCatalog.TryCreate(
                    reversedInput,
                    composition.Plan,
                    out GameplayTriggerCombatSensorCatalog reversed,
                    out GameplayTriggerCombatSensorCatalogResult reversedResult),
                Is.True,
                Describe(reversedResult));
            Assert.That(
                GameplayTriggerCombatSensorCatalog.TryCreate(
                    new[] { first, second },
                    composition.Plan,
                    out GameplayTriggerCombatSensorCatalog forward,
                    out GameplayTriggerCombatSensorCatalogResult forwardResult),
                Is.True,
                Describe(forwardResult));

            Assert.That(reversed.GetDescriptor(0).SourceBindingId, Is.EqualTo(SourceBinding(1)));
            Assert.That(reversed.GetDescriptor(1).SourceBindingId, Is.EqualTo(SourceBinding(2)));
            Assert.That(reversed.SensorCatalogHash, Is.EqualTo(forward.SensorCatalogHash));
            Assert.That(reversed.CombinedCatalogHash, Is.EqualTo(forward.CombinedCatalogHash));
            Assert.That(reversed.GameplayTriggerCatalogHash, Is.EqualTo(composition.Plan.GameplayTriggerCatalogHash));

            long frozenHash = reversed.SensorCatalogHash;
            reversedInput[0] = Descriptor(2, 44, SectorQuery(999), 8, 4);
            Assert.That(reversed.GetDescriptor(1), Is.EqualTo(second));
            Assert.That(reversed.SensorCatalogHash, Is.EqualTo(frozenHash));
        }

        [Test]
        public void CatalogHash_EveryEffectiveFieldHasMutationOracle()
        {
            TestComposition composition = CreateComposition();
            CombatPhysicsQuery sphere = HashSphereQuery();
            GameplayTriggerCombatSensorCatalog baseline = CreateCatalog(
                composition.Plan,
                new[] { Descriptor(1, 7, sphere, 4, 4) });

            TestComposition secondSource = CreateComposition(bindingCount: 2);
            AssertSensorHashChanged(
                baseline,
                secondSource.Plan,
                Descriptor(2, 7, sphere, 4, 4),
                "SourceBindingId");
            TestComposition secondProvider = CreateComposition(providerId: 8);
            AssertSensorHashChanged(
                baseline,
                secondProvider.Plan,
                Descriptor(1, 8, sphere, 4, 4),
                "ProviderId");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, sphere, 5, 4), "MaxQueryHits");
            TestComposition mappedCapacity = CreateComposition(maxMappedSubjects: 5);
            AssertSensorHashChanged(
                baseline,
                mappedCapacity.Plan,
                Descriptor(1, 7, sphere, 4, 5),
                "MaxMappedSubjects");

            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(queryId: 2), 4, 4), "Header.QueryId");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(sourceEntityId: 31), 4, 4), "Header.SourceEntityId");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(traceId: 32), 4, 4), "Header.TraceId");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(actionId: 33), 4, 4), "Header.ActionId");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(sourceOrder: 34), 4, 4), "Header.SourceOrder");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(layer: Layer + 1), 4, 4), "Header.LayerMask");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(includeSource: true), 4, 4), "Filter.IncludeSourceEntity");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(centerX: 1), 4, 4), "Sphere.Center");
            AssertSensorHashChanged(baseline, composition.Plan, Descriptor(1, 7, HashSphereQuery(radius: 6), 4, 4), "Sphere.Radius");

            GameplayTriggerCombatSensorCatalog capsule = CreateCatalog(
                composition.Plan,
                new[] { Descriptor(1, 7, HashCapsuleQuery(), 4, 4) });
            Assert.That(capsule.SensorCatalogHash, Is.Not.EqualTo(baseline.SensorCatalogHash), "Shape.Kind/Header.Kind");
            AssertSensorHashChanged(capsule, composition.Plan, Descriptor(1, 7, HashCapsuleQuery(pointAX: -4), 4, 4), "Capsule.PointA");
            AssertSensorHashChanged(capsule, composition.Plan, Descriptor(1, 7, HashCapsuleQuery(pointBX: 4), 4, 4), "Capsule.PointB");
            AssertSensorHashChanged(capsule, composition.Plan, Descriptor(1, 7, HashCapsuleQuery(radius: 6), 4, 4), "Capsule.Radius");

            GameplayTriggerCombatSensorCatalog aabb = CreateCatalog(
                composition.Plan,
                new[] { Descriptor(1, 7, HashAabbQuery(), 4, 4) });
            AssertSensorHashChanged(aabb, composition.Plan, Descriptor(1, 7, HashAabbQuery(minX: -4), 4, 4), "Aabb.Min");
            AssertSensorHashChanged(aabb, composition.Plan, Descriptor(1, 7, HashAabbQuery(maxX: 4), 4, 4), "Aabb.Max");

            GameplayTriggerCombatSensorCatalog sector = CreateCatalog(
                composition.Plan,
                new[] { Descriptor(1, 7, HashSectorQuery(), 4, 4) });
            AssertSensorHashChanged(sector, composition.Plan, Descriptor(1, 7, HashSectorQuery(originX: 1), 4, 4), "Sector.Origin");
            AssertSensorHashChanged(sector, composition.Plan, Descriptor(1, 7, HashSectorQuery(directionY: 1), 4, 4), "Sector.Direction");
            AssertSensorHashChanged(sector, composition.Plan, Descriptor(1, 7, HashSectorQuery(radius: 6), 4, 4), "Sector.Radius");
            AssertSensorHashChanged(sector, composition.Plan, Descriptor(1, 7, HashSectorQuery(minDot: Fix64.Half), 4, 4), "Sector.MinDot");

            var enemyFilter = new GameplayComponentTargetFilter(
                GameplayTargetIdentityTypes.Character,
                GameplayTargetCapabilities.Damageable,
                requireAlive: true,
                GameplayTargetRelationFilter.Enemy,
                referenceTeamId: 1);
            GameplayTriggerCombatSensorCatalog filtered = CreateCatalog(
                composition.Plan,
                new[]
                {
                    Descriptor(1, 7, sphere, 4, 4, enemyFilter)
                });
            Assert.That(
                filtered.SensorCatalogHash,
                Is.Not.EqualTo(baseline.SensorCatalogHash),
                "SubjectFilter");
            AssertSensorHashChanged(
                filtered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.WorldObject,
                        GameplayTargetCapabilities.Damageable,
                        requireAlive: true,
                        GameplayTargetRelationFilter.Enemy,
                        referenceTeamId: 1)),
                "SubjectFilter.AcceptedIdentityTypes");
            AssertSensorHashChanged(
                filtered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Movable,
                        requireAlive: true,
                        GameplayTargetRelationFilter.Enemy,
                        referenceTeamId: 1)),
                "SubjectFilter.RequiredCapabilities");
            AssertSensorHashChanged(
                filtered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable,
                        requireAlive: false,
                        GameplayTargetRelationFilter.Enemy,
                        referenceTeamId: 1)),
                "SubjectFilter.RequireAlive");
            AssertSensorHashChanged(
                filtered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable,
                        requireAlive: true,
                        GameplayTargetRelationFilter.SameTeam,
                        referenceTeamId: 1)),
                "SubjectFilter.RelationFilter");
            AssertSensorHashChanged(
                filtered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable,
                        requireAlive: true,
                        GameplayTargetRelationFilter.Enemy,
                        referenceTeamId: 2)),
                "SubjectFilter.ReferenceTeamId");

            GameplayTriggerCombatSensorCatalog selfFiltered = CreateCatalog(
                composition.Plan,
                new[]
                {
                    Descriptor(
                        1,
                        7,
                        sphere,
                        4,
                        4,
                        new GameplayComponentTargetFilter(
                            GameplayTargetIdentityTypes.Character,
                            GameplayTargetCapabilities.None,
                            requireAlive: true,
                            GameplayTargetRelationFilter.Self,
                            new GameplayEntityId(1, 1)))
                });
            AssertSensorHashChanged(
                selfFiltered,
                composition.Plan,
                Descriptor(
                    1,
                    7,
                    sphere,
                    4,
                    4,
                    new GameplayComponentTargetFilter(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.None,
                        requireAlive: true,
                        GameplayTargetRelationFilter.Self,
                        new GameplayEntityId(2, 1))),
                "SubjectFilter.ReferenceEntityId");

            GameplayTriggerCombatSensorCatalog sameSensorDifferentTrigger = CreateCatalog(
                secondSource.Plan,
                new[] { Descriptor(1, 7, sphere, 4, 4) });
            Assert.That(sameSensorDifferentTrigger.SensorCatalogHash, Is.EqualTo(baseline.SensorCatalogHash));
            Assert.That(sameSensorDifferentTrigger.GameplayTriggerCatalogHash, Is.Not.EqualTo(baseline.GameplayTriggerCatalogHash));
            Assert.That(sameSensorDifferentTrigger.CombinedCatalogHash, Is.Not.EqualTo(baseline.CombinedCatalogHash));
        }

        [Test]
        public void Catalog_InvalidContractsShapesFiltersAndCapacities_FailClosed()
        {
            TestComposition composition = CreateComposition();
            GameplayTriggerCombatSensorDescriptor valid = Descriptor(1, 7, SphereQuery(1), 4, 4);
            AssertCatalogFailure(
                composition.Plan,
                new[] { valid, valid },
                GameplayTriggerCombatSensorCatalogErrorCode.DuplicateSourceBinding);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(2, 7, SphereQuery(1), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.SourceRequirementMissing);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 8, SphereQuery(1), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.ProviderMismatch);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, SphereQuery(1), 0, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, SphereQuery(1), 4, 3) },
                GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch);
            TestComposition insufficientRelations = CreateComposition(
                maxMappedSubjects: 2,
                maxActiveRelations: 1);
            AssertCatalogFailure(
                insufficientRelations.Plan,
                new[] { Descriptor(1, 7, SphereQuery(1), 4, 2) },
                GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, RayQuery(1), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedShape);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, ObbQuery(1), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedShape);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, default, 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedShape);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, SphereQuery(1, CombatPhysicsLayerMask.None), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.EmptyLayerMask);
            AssertCatalogFailure(
                composition.Plan,
                new[] { Descriptor(1, 7, SphereQueryWithInertFilter(1), 4, 4) },
                GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedFilter);

            TestComposition instigator = CreateComposition(
                capabilities: RequiredCapabilities |
                    GameplayTriggerSourceEntityCapability.InstigatorPossible);
            AssertCatalogFailure(
                instigator.Plan,
                new[] { valid },
                GameplayTriggerCombatSensorCatalogErrorCode.EntityCapabilityMismatch);
            TestComposition threshold = CreateComposition(sourceKind: GameplayTriggerSourceKind.Threshold);
            AssertCatalogFailure(
                threshold.Plan,
                new[] { valid },
                GameplayTriggerCombatSensorCatalogErrorCode.SourceKindMismatch);
        }

        [Test]
        public void SystemInitialization_RejectsInvalidCompositionAndFreezesSchedule()
        {
            TestComposition composition = CreateComposition();
            GameplayTriggerCombatSensorCatalog catalog = CreateCatalog(
                composition.Plan,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var componentWorld = new GameplayComponentWorld();

            Assert.That(
                GameplayTriggerCombatSensorSystem.TryCreate(
                    catalog,
                    composition.Runtime,
                    physicsWorld,
                    map,
                    componentWorld,
                    GameplayTriggerCombatSensorSystem.DefaultSystemId,
                    GameplayTriggerEvaluationSystem.DefaultPriority,
                    out GameplayTriggerCombatSensorSystem invalidPriority,
                    out GameplayTriggerCombatSensorInitializationResult priorityResult),
                Is.False);
            Assert.That(invalidPriority, Is.Null);
            Assert.That(priorityResult.ErrorCode, Is.EqualTo(GameplayTriggerCombatSensorInitializationErrorCode.InvalidPriority));

            Assert.That(
                GameplayTriggerCombatSensorSystem.TryCreate(
                    catalog,
                    composition.Runtime,
                    physicsWorld,
                    map,
                    componentWorld,
                    " ",
                    GameplayTriggerCombatSensorSystem.DefaultPriority,
                    out GameplayTriggerCombatSensorSystem invalidId,
                    out GameplayTriggerCombatSensorInitializationResult idResult),
                Is.False);
            Assert.That(invalidId, Is.Null);
            Assert.That(idResult.ErrorCode, Is.EqualTo(GameplayTriggerCombatSensorInitializationErrorCode.InvalidSystemId));

            TestComposition otherComposition = CreateComposition(bindingCount: 2);
            Assert.That(otherComposition.Runtime.GameplayTriggerCatalogHash, Is.Not.EqualTo(catalog.GameplayTriggerCatalogHash));
            Assert.That(
                GameplayTriggerCombatSensorSystem.TryCreate(
                    catalog,
                    otherComposition.Runtime,
                    physicsWorld,
                    map,
                    componentWorld,
                    out GameplayTriggerCombatSensorSystem catalogMismatch,
                    out GameplayTriggerCombatSensorInitializationResult catalogResult),
                Is.False);
            Assert.That(catalogMismatch, Is.Null);
            Assert.That(catalogResult.ErrorCode, Is.EqualTo(GameplayTriggerCombatSensorInitializationErrorCode.RuntimeCatalogMismatch));

            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });
            Assert.That(system.Phase, Is.EqualTo(GameplaySystemPhase.Simulation));
            Assert.That(system.Priority, Is.EqualTo(950));
            Assert.That(system.Priority, Is.LessThan(GameplayTriggerEvaluationSystem.DefaultPriority));
        }

        [Test]
        public void Shapes_SphereCapsuleAabbAndSector_SubmitOneSubject()
        {
            CombatPhysicsQuery[] queries =
            {
                SphereQuery(1),
                CapsuleQuery(2),
                AabbQuery(3),
                SectorQuery(4)
            };

            for (int i = 0; i < queries.Length; i++)
            {
                TestComposition composition = CreateComposition();
                var componentWorld = new GameplayComponentWorld();
                GameplayEntityId subject = componentWorld.CreateEntity();
                var physicsWorld = new CombatPhysicsWorld();
                AddCollider(physicsWorld, combatValue: 1, bodyValue: 1, colliderValue: 1, x: 1);
                var map = new CombatEntityGameplayMap();
                map.Register(new CombatEntityId(1), subject);
                GameplayTriggerCombatSensorSystem system = CreateSystem(
                    composition,
                    physicsWorld,
                    map,
                    componentWorld,
                    new[] { Descriptor(1, 7, queries[i], 1, 4) });

                system.Tick(Context(RuntimeFrame.Zero, componentWorld));

                Assert.That(system.LastTickSummary.QueryCount, Is.EqualTo(1), queries[i].Shape.Kind.ToString());
                Assert.That(system.LastTickSummary.SubmittedSubjectCount, Is.EqualTo(1));
                Assert.That(system.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
            }
        }

        [Test]
        public void ContextMismatchAndRepeatedFrame_OverwriteAllRowsAndNeverQuery()
        {
            TestComposition composition = CreateComposition(bindingCount: 2);
            var frozenWorld = new GameplayComponentWorld();
            var wrongWorld = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                new CombatEntityGameplayMap(),
                frozenWorld,
                new[]
                {
                    Descriptor(1, 7, SphereQuery(1), 1, 4),
                    Descriptor(2, 7, SphereQuery(2), 1, 4)
                });

            system.Tick(Context(RuntimeFrame.Zero, wrongWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.ContextMismatch);
            Assert.That(system.LastTickSummary.QueryCount, Is.Zero);
            system.Tick(Context(RuntimeFrame.Zero, frozenWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.DuplicateOrRegressedFrame);
            Assert.That(system.LastTickSummary.QueryCount, Is.Zero);

            system.ResetSchedulingState();
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.None);
            system.Tick(Context(RuntimeFrame.Zero, frozenWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.Submitted);
            Assert.That(system.LastTickSummary.QueryCount, Is.EqualTo(2));
        }

        [Test]
        public void DisabledSource_PerformsZeroQueryAndZeroSubmit()
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            Assert.That(
                composition.Runtime.TrySetEnabled(
                    composition.Runtime.GetInstanceId(0),
                    false,
                    out GameplayTriggerResult setResult),
                Is.True,
                setResult.ErrorCode.ToString());
            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                new CombatEntityGameplayMap(),
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });

            system.Tick(Context(RuntimeFrame.Zero, componentWorld));

            Assert.That(system.LastTickSummary.QueryCount, Is.Zero);
            Assert.That(system.LastTickSummary.SubmittedBatchCount, Is.Zero);
            Assert.That(system.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Disabled));
        }

        [Test]
        public void DisabledSystem_ConsumesFrameAndCannotRetryAfterReenable()
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                new CombatEntityGameplayMap(),
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });

            system.SetEnabled(false);
            system.Tick(Context(RuntimeFrame.Zero, componentWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.Disabled);
            Assert.That(system.LastTickSummary.QueryCount, Is.Zero);

            system.SetEnabled(true);
            system.Tick(Context(RuntimeFrame.Zero, componentWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.DuplicateOrRegressedFrame);
            Assert.That(system.LastTickSummary.QueryCount, Is.Zero);

            system.Tick(Context(new RuntimeFrame(1), componentWorld));
            AssertAllRows(system, GameplayTriggerCombatSensorTickStatus.Submitted);
            Assert.That(system.LastTickSummary.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public void Mapping_DeduplicatesCombatAndGameplayAndFiltersUnmappedAndStale()
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId first = componentWorld.CreateEntity();
            GameplayEntityId second = componentWorld.CreateEntity();
            GameplayEntityId stale = componentWorld.CreateEntity();
            Assert.That(componentWorld.DestroyEntity(stale), Is.True);
            GameplayEntityId replacement = componentWorld.CreateEntity();
            Assert.That(replacement.Index, Is.EqualTo(stale.Index));
            Assert.That(replacement.Generation, Is.Not.EqualTo(stale.Generation));
            Assert.That(componentWorld.IsAlive(stale), Is.False);
            Assert.That(componentWorld.IsAlive(replacement), Is.True);
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            AddAdditionalCollider(physicsWorld, 1, 2);
            AddCollider(physicsWorld, 2, 2, 1, 1);
            AddCollider(physicsWorld, 3, 3, 1, 2);
            AddCollider(physicsWorld, 4, 4, 1, 3);
            AddCollider(physicsWorld, 5, 5, 1, 4);
            var reader = new FixedMapReader(
                new[] { 1, 2, 4, 5 },
                new[] { first, first, stale, second });
            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                reader,
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 6, 4) });

            system.Tick(Context(RuntimeFrame.Zero, componentWorld));
            GameplayTriggerCombatSensorBindingTickResult row = system.GetLastBindingResult(0);

            Assert.That(row.Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
            Assert.That(row.QueryHitCount, Is.EqualTo(6));
            Assert.That(row.SubmittedSubjectCount, Is.EqualTo(2));
            Assert.That(row.DuplicateCombatHitCount, Is.EqualTo(1));
            Assert.That(row.DuplicateGameplaySubjectCount, Is.EqualTo(1));
            Assert.That(row.UnmappedCombatEntityCount, Is.EqualTo(1));
            Assert.That(row.StaleGameplayEntityCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidMappedId_IsAtomicAndOtherSourceStillSubmits()
        {
            TestComposition composition = CreateComposition(bindingCount: 2);
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId valid = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            AddCollider(physicsWorld, 2, 2, 2, 50);
            var reader = new OneInvalidMapReader(valid);
            GameplayTriggerCombatSensorSystem system = CreateSystem(
                composition,
                physicsWorld,
                reader,
                componentWorld,
                new[]
                {
                    Descriptor(1, 7, CenteredSphereQuery(1, 0, 5), 2, 4),
                    Descriptor(2, 7, CenteredSphereQuery(2, 50, 5), 2, 4)
                });

            system.Tick(Context(RuntimeFrame.Zero, componentWorld));

            Assert.That(system.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.InvalidMappedGameplayEntity));
            Assert.That(system.GetLastBindingResult(0).SubmittedSubjectCount, Is.Zero);
            Assert.That(system.GetLastBindingResult(1).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
            Assert.That(system.LastTickSummary.SubmittedBatchCount, Is.EqualTo(1));
            Assert.That(composition.Runtime.Evaluate(RuntimeFrame.Zero).Success, Is.True);
            Assert.That(composition.Runtime.RelationCount, Is.EqualTo(1));
        }

        [Test]
        public void EquivalentHitOrders_ProduceSameOccurrenceActionAndAuthorityHash()
        {
            OrderRun left = RunOrderScenario(firstX: 1, secondX: 5, firstBody: 10, secondBody: 20);
            OrderRun right = RunOrderScenario(firstX: 5, secondX: 1, firstBody: 40, secondBody: 30);

            Assert.That(left.FirstSubject, Is.EqualTo(right.FirstSubject));
            Assert.That(left.SecondSubject, Is.EqualTo(right.SecondSubject));
            Assert.That(left.FirstActionTarget, Is.EqualTo(right.FirstActionTarget));
            Assert.That(left.SecondActionTarget, Is.EqualTo(right.SecondActionTarget));
            Assert.That(left.AuthorityHash, Is.EqualTo(right.AuthorityHash));
        }

        [Test]
        public void EmptyA_AEmpty_ProducesEnteredStayedExitedWithOneCompleteSubmitPerFrame()
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId subject = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), subject);
            GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });
            var evaluation = new GameplayTriggerEvaluationSystem(composition.Runtime);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(evaluation);
            pipeline.Add(sensor);
            Assert.That(sensor.Priority, Is.LessThan(evaluation.Priority));

            pipeline.Tick(Context(new RuntimeFrame(0), componentWorld));
            Assert.That(composition.Runtime.OccurrenceCount, Is.Zero);
            AddCollider(physicsWorld, 1, 1, 1, 0);
            pipeline.Tick(Context(new RuntimeFrame(1), componentWorld));
            AssertSingleOccurrence(composition.Runtime, GameplayTriggerOccurrenceKind.Entered, subject);
            pipeline.Tick(Context(new RuntimeFrame(2), componentWorld));
            AssertSingleOccurrence(composition.Runtime, GameplayTriggerOccurrenceKind.Stayed, subject);
            Assert.That(physicsWorld.RemoveCollider(new CombatBodyId(1), new CombatColliderId(1)), Is.True);
            pipeline.Tick(Context(new RuntimeFrame(3), componentWorld));
            AssertSingleOccurrence(composition.Runtime, GameplayTriggerOccurrenceKind.Exited, subject);
            Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
        }

        [Test]
        public void CapacityBounds_NMinusOneNAndNPlusOne_AreAtomic()
        {
            for (int colliderCount = 1; colliderCount <= 3; colliderCount++)
            {
                TestComposition composition = CreateComposition(maxMappedSubjects: 3);
                var componentWorld = new GameplayComponentWorld();
                var physicsWorld = new CombatPhysicsWorld();
                var map = new CombatEntityGameplayMap();
                for (int i = 1; i <= colliderCount; i++)
                {
                    GameplayEntityId subject = componentWorld.CreateEntity();
                    AddCollider(physicsWorld, i, i, 1, i);
                    map.Register(new CombatEntityId(i), subject);
                }
                GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                    composition,
                    physicsWorld,
                    map,
                    componentWorld,
                    new[] { Descriptor(1, 7, SphereQuery(1), 2, 3) });

                sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));

                if (colliderCount <= 2)
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
                    Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.EqualTo(colliderCount));
                }
                else
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.QueryCapacityExceeded));
                    Assert.That(sensor.LastTickSummary.QueryCount, Is.Zero);
                    Assert.That(composition.Runtime.RelationCount, Is.Zero);
                }
            }
        }

        [Test]
        public void MappedAndTriggerInputCapacity_NMinusOneNAndNPlusOne_AreAtomic()
        {
            for (int subjectCount = 1; subjectCount <= 3; subjectCount++)
            {
                TestComposition composition = CreateComposition(maxMappedSubjects: 2);
                var componentWorld = new GameplayComponentWorld();
                var physicsWorld = new CombatPhysicsWorld();
                var map = new CombatEntityGameplayMap();
                for (int i = 1; i <= subjectCount; i++)
                {
                    GameplayEntityId subject = componentWorld.CreateEntity();
                    AddCollider(physicsWorld, i, i, 1, i);
                    map.Register(new CombatEntityId(i), subject);
                }
                GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                    composition,
                    physicsWorld,
                    map,
                    componentWorld,
                    new[] { Descriptor(1, 7, SphereQuery(1), 3, 2) });

                sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));

                if (subjectCount <= 2)
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
                    Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.EqualTo(subjectCount));
                }
                else
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.MappedSubjectCapacityExceeded));
                    Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.Zero);
                    Assert.That(composition.Runtime.RelationCount, Is.Zero);
                }
            }

            for (int subjectCount = 0; subjectCount <= 2; subjectCount++)
            {
                TestComposition composition = CreateComposition(maxMappedSubjects: 2);
                GameplayTriggerRuntime runtime = CreateRuntimeWithInputCapacity(composition, 1);
                var componentWorld = new GameplayComponentWorld();
                var physicsWorld = new CombatPhysicsWorld();
                var map = new CombatEntityGameplayMap();
                for (int i = 1; i <= subjectCount; i++)
                {
                    GameplayEntityId subject = componentWorld.CreateEntity();
                    AddCollider(physicsWorld, i, i, 1, i);
                    map.Register(new CombatEntityId(i), subject);
                }
                GameplayTriggerCombatSensorCatalog catalog = CreateCatalog(
                    composition.Plan,
                    new[] { Descriptor(1, 7, SphereQuery(1), 2, 2) });
                Assert.That(
                    GameplayTriggerCombatSensorSystem.TryCreate(
                        catalog,
                        runtime,
                        physicsWorld,
                        map,
                        componentWorld,
                        out GameplayTriggerCombatSensorSystem sensor,
                        out GameplayTriggerCombatSensorInitializationResult init),
                    Is.True,
                    Describe(init));

                sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));

                if (subjectCount <= 1)
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
                    Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.EqualTo(subjectCount));
                }
                else
                {
                    Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.TriggerSubmitRejected));
                    Assert.That(sensor.GetLastBindingResult(0).TriggerErrorCode, Is.EqualTo(GameplayTriggerErrorCode.InputCapacityExceeded));
                    Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.Zero);
                    Assert.That(runtime.RelationCount, Is.Zero);
                }
            }
        }

        [TestCase(SensorFailureMode.QueryCapacity)]
        [TestCase(SensorFailureMode.MappedSubjectCapacity)]
        [TestCase(SensorFailureMode.TriggerInputCapacity)]
        public void FailedFrame_PreservesOldSourceTruthAndResumesWithStayed(
            SensorFailureMode failureMode)
        {
            int compiledSubjectCapacity = failureMode == SensorFailureMode.MappedSubjectCapacity ? 1 : 2;
            TestComposition composition = CreateComposition(maxMappedSubjects: compiledSubjectCapacity);
            GameplayTriggerRuntime runtime = failureMode == SensorFailureMode.TriggerInputCapacity
                ? CreateRuntimeWithInputCapacity(composition, 1)
                : composition.Runtime;
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId first = componentWorld.CreateEntity();
            GameplayEntityId second = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), first);
            map.Register(new CombatEntityId(2), second);
            int maxQueryHits = failureMode == SensorFailureMode.QueryCapacity ? 1 : 2;
            GameplayTriggerCombatSensorCatalog catalog = CreateCatalog(
                composition.Plan,
                new[]
                {
                    Descriptor(
                        1,
                        7,
                        SphereQuery(1),
                        maxQueryHits,
                        compiledSubjectCapacity)
                });
            Assert.That(
                GameplayTriggerCombatSensorSystem.TryCreate(
                    catalog,
                    runtime,
                    physicsWorld,
                    map,
                    componentWorld,
                    out GameplayTriggerCombatSensorSystem sensor,
                    out GameplayTriggerCombatSensorInitializationResult init),
                Is.True,
                Describe(init));

            sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));
            Assert.That(runtime.Evaluate(RuntimeFrame.Zero).Success, Is.True);
            AssertSingleOccurrence(runtime, GameplayTriggerOccurrenceKind.Entered, first);
            Assert.That(runtime.RelationCount, Is.EqualTo(1));

            AddCollider(physicsWorld, 2, 2, 1, 1);
            sensor.Tick(Context(new RuntimeFrame(1), componentWorld));
            GameplayTriggerCombatSensorTickStatus expectedFailure = failureMode == SensorFailureMode.QueryCapacity
                ? GameplayTriggerCombatSensorTickStatus.QueryCapacityExceeded
                : failureMode == SensorFailureMode.MappedSubjectCapacity
                    ? GameplayTriggerCombatSensorTickStatus.MappedSubjectCapacityExceeded
                    : GameplayTriggerCombatSensorTickStatus.TriggerSubmitRejected;
            Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(expectedFailure));
            Assert.That(sensor.GetLastBindingResult(0).SubmittedSubjectCount, Is.Zero);
            Assert.That(runtime.Evaluate(new RuntimeFrame(1)).Success, Is.True);
            Assert.That(runtime.RelationCount, Is.EqualTo(1));
            Assert.That(runtime.OccurrenceCount, Is.Zero);

            Assert.That(
                physicsWorld.RemoveCollider(new CombatBodyId(2), new CombatColliderId(1)),
                Is.True);
            sensor.Tick(Context(new RuntimeFrame(2), componentWorld));
            Assert.That(sensor.GetLastBindingResult(0).Status, Is.EqualTo(GameplayTriggerCombatSensorTickStatus.Submitted));
            Assert.That(runtime.Evaluate(new RuntimeFrame(2)).Success, Is.True);
            Assert.That(runtime.RelationCount, Is.EqualTo(1));
            AssertSingleOccurrence(runtime, GameplayTriggerOccurrenceKind.Stayed, first);
        }

        [Test]
        public void SubjectFilter_RunsBeforeOccupancyAndTeamChangeProducesExited()
        {
            TestComposition composition =
                CreateComposition(maxMappedSubjects: 2);
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId enemy = componentWorld.CreateEntity();
            GameplayEntityId ally = componentWorld.CreateEntity();
            GameplayComponentStore<GameplayTargetClassificationComponent>
                classifications = componentWorld
                    .GetOrCreateStore<
                        GameplayTargetClassificationComponent>();
            GameplayComponentStore<GameplayTeamComponent> teams =
                componentWorld.GetOrCreateStore<GameplayTeamComponent>();
            GameplayComponentStore<GameplayLifecycleComponent> lifecycles =
                componentWorld
                    .GetOrCreateStore<GameplayLifecycleComponent>();
            var classification =
                new GameplayTargetClassificationComponent(
                    GameplayTargetIdentityTypes.Character,
                    GameplayTargetCapabilities.Damageable);
            classifications.Set(enemy, classification);
            classifications.Set(ally, classification);
            teams.Set(enemy, new GameplayTeamComponent(2));
            teams.Set(ally, new GameplayTeamComponent(1));
            lifecycles.Set(enemy, GameplayLifecycleComponent.Alive);
            lifecycles.Set(ally, GameplayLifecycleComponent.Alive);

            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            AddCollider(physicsWorld, 2, 2, 1, 1);
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), enemy);
            map.Register(new CombatEntityId(2), ally);
            var filter = new GameplayComponentTargetFilter(
                GameplayTargetIdentityTypes.Character,
                GameplayTargetCapabilities.Damageable,
                requireAlive: true,
                GameplayTargetRelationFilter.Enemy,
                referenceTeamId: 1);
            GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[]
                {
                    Descriptor(
                        1,
                        7,
                        SphereQuery(1),
                        2,
                        2,
                        filter)
                });

            sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));
            GameplayTriggerCombatSensorBindingTickResult first =
                sensor.GetLastBindingResult(0);
            Assert.That(first.SubmittedSubjectCount, Is.EqualTo(1));
            Assert.That(first.FilteredSubjectCount, Is.EqualTo(1));
            Assert.That(
                first.FirstFilterRejectReason,
                Is.EqualTo(GameplayTargetRejectReason.SameTeam));
            Assert.That(
                composition.Runtime.Evaluate(RuntimeFrame.Zero).Success,
                Is.True);
            AssertSingleOccurrence(
                composition.Runtime,
                GameplayTriggerOccurrenceKind.Entered,
                enemy);

            teams.Set(enemy, new GameplayTeamComponent(1));
            sensor.Tick(Context(new RuntimeFrame(1), componentWorld));
            GameplayTriggerCombatSensorBindingTickResult second =
                sensor.GetLastBindingResult(0);
            Assert.That(second.SubmittedSubjectCount, Is.Zero);
            Assert.That(second.FilteredSubjectCount, Is.EqualTo(2));
            Assert.That(
                composition.Runtime.Evaluate(new RuntimeFrame(1)).Success,
                Is.True);
            AssertSingleOccurrence(
                composition.Runtime,
                GameplayTriggerOccurrenceKind.Exited,
                enemy);
        }

        [Test]
        public void InPlaceRestore_ResetSchedulingState_ContinuesWithoutDuplicateEntered()
        {
            RestoreRun baseline = CreateRestoreRun();
            TickAndEvaluate(baseline, 0);
            TickAndEvaluate(baseline, 1);
            TickAndEvaluate(baseline, 2);
            GameplayTriggerOccurrence baselineOccurrence = baseline.Composition.Runtime.GetOccurrence(0);
            long baselineActivation = baseline.Composition.Runtime.GetActivation(0).ActivationId;
            long baselineHash = ComputeAuthorityHash(baseline.Composition.Runtime, new RuntimeFrame(2));

            RestoreRun interrupted = CreateRestoreRun();
            TickAndEvaluate(interrupted, 0);
            TickAndEvaluate(interrupted, 1);
            var participant = new GameplayTriggerRuntimeSaveStateParticipant(
                interrupted.Composition.Runtime,
                new WorldValidator(interrupted.ComponentWorld));
            RuntimeSaveStateResult<RuntimeSaveState> capture = participant.CaptureSaveState();
            Assert.That(capture.Success, Is.True, capture.Error?.Message);
            TickAndEvaluate(interrupted, 5);
            RuntimeSaveStateResult<bool> restore = participant.RestoreSaveState(capture.Value);
            Assert.That(restore.Success, Is.True, restore.Error?.Message);
            interrupted.Sensor.ResetSchedulingState();
            TickAndEvaluate(interrupted, 2);

            Assert.That(interrupted.Composition.Runtime.GetOccurrence(0).Kind, Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));
            Assert.That(interrupted.Composition.Runtime.GetOccurrence(0).Kind, Is.EqualTo(baselineOccurrence.Kind));
            Assert.That(interrupted.Composition.Runtime.GetActivation(0).ActivationId, Is.EqualTo(baselineActivation));
            Assert.That(
                ComputeAuthorityHash(interrupted.Composition.Runtime, new RuntimeFrame(2)),
                Is.EqualTo(baselineHash));
        }

        [Test]
        public void MultiSensor_SixtyFourTicksRemainNoAllocAfterCombatAndBridgeWarmup()
        {
            TestComposition composition = CreateComposition(bindingCount: 2);
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId first = componentWorld.CreateEntity();
            GameplayEntityId second = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            AddCollider(physicsWorld, 2, 2, 1, 1);
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), first);
            map.Register(new CombatEntityId(2), second);
            GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[]
                {
                    Descriptor(1, 7, SphereQuery(1), 2, 4),
                    Descriptor(2, 7, AabbQuery(2), 2, 4)
                });
            var evaluation = new GameplayTriggerEvaluationSystem(composition.Runtime);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(sensor);
            pipeline.Add(evaluation);
            var gameplayWorld = new GameplayWorld();
            var commandState = new GameplayCommandExecutionState();

            pipeline.Tick(Context(RuntimeFrame.Zero, componentWorld, gameplayWorld, commandState));
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 1; frame <= 64; frame++)
            {
                pipeline.Tick(Context(new RuntimeFrame(frame), componentWorld, gameplayWorld, commandState));
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(sensor.LastTickSummary.SubmittedBatchCount, Is.EqualTo(2));
            Assert.That(sensor.LastTickSummary.SubmittedSubjectCount, Is.EqualTo(4));
        }

        private static GameplayTriggerSourceEntityCapability RequiredCapabilities =>
            GameplayTriggerSourceEntityCapability.SubjectPossible |
            GameplayTriggerSourceEntityCapability.SubjectGuaranteed;

        public enum SensorFailureMode
        {
            QueryCapacity,
            MappedSubjectCapacity,
            TriggerInputCapacity
        }

        private static TestComposition CreateComposition(
            int bindingCount = 1,
            int maxMappedSubjects = 4,
            int? maxActiveRelations = null,
            GameplayTriggerSourceEntityCapability? capabilities = null,
            int providerId = 7,
            GameplayTriggerSourceKind sourceKind = GameplayTriggerSourceKind.Occupancy)
        {
            GameplayTriggerSourceEntityCapability actualCapabilities = capabilities ?? RequiredCapabilities;
            var bindings = new GameplayTriggerBindingAuthoring[bindingCount];
            var sourceContracts = new GameplayTriggerSourceBindingContractDescriptor[bindingCount];
            for (int i = 0; i < bindingCount; i++)
            {
                int id = i + 1;
                bindings[i] = new GameplayTriggerBindingAuthoring(id, 1, id);
                sourceContracts[i] = new GameplayTriggerSourceBindingContractDescriptor(
                    SourceBinding(id),
                    new GameplayTriggerSourceDefinitionId(1),
                    sourceKind,
                    new GameplayTriggerSourceProviderId(providerId),
                    actualCapabilities,
                    true,
                    maxMappedSubjects,
                    maxActiveRelations ?? maxMappedSubjects,
                    signalEnvelopeSchemaVersion: 0,
                    signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset,
                    preservesOrderedSequence: false);
            }

            var source = new GameplayTriggerSourceAuthoring(
                1,
                sourceKind,
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive,
                departurePolicy: GameplayTriggerSubjectDeparturePolicy.EmitExited);
            var condition = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue));
            var route = new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Entered |
                    GameplayTriggerOccurrenceKind.Stayed |
                    GameplayTriggerOccurrenceKind.Exited,
                1,
                new[] { new GameplayTriggerActionAuthoring(ActionKindId.Value, GameplayTriggerTargetSelector.Subject) },
                stateScope: GameplayTriggerStateScope.PerSubject,
                maxStateKeys: maxMappedSubjects);
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { source },
                new[] { condition },
                new[] { new GameplayTriggerDefinitionAuthoring(1, 1, 1, new[] { route }) },
                bindings);
            Assert.That(
                GameplayTriggerRetiredIdRegistry.TryCreate(
                    null,
                    out GameplayTriggerRetiredIdRegistry retired,
                    out GameplayTriggerConfigRegistryResult retiredResult),
                Is.True,
                Describe(retiredResult));
            Assert.That(
                GameplayTriggerSourceBindingContractRegistry.TryCreate(
                    sourceContracts,
                    out GameplayTriggerSourceBindingContractRegistry sourceRegistry,
                    out GameplayTriggerConfigRegistryResult sourceResult),
                Is.True,
                Describe(sourceResult));
            Assert.That(
                GameplayTriggerActionRequirementRegistry.TryCreate(
                    new[]
                    {
                        new GameplayTriggerActionRequirementDescriptor(
                            ActionKindId,
                            GameplayTriggerDeliveryContract.SameFrameReserved)
                    },
                    out GameplayTriggerActionRequirementRegistry actionRegistry,
                    out GameplayTriggerConfigRegistryResult actionResult),
                Is.True,
                Describe(actionResult));
            var expected = new GameplayTriggerExpectedCatalogIdentity(
                GameplayTriggerConfigSchemas.RuntimeCatalogId,
                GameplayTriggerConfigSchemas.RuntimeCatalogVersion);
            Assert.That(
                GameplayTriggerCompilerEnvironment.TryCreate(
                    retired,
                    sourceRegistry,
                    actionRegistry,
                    expected,
                    out GameplayTriggerCompilerEnvironment environment,
                    out GameplayTriggerConfigRegistryResult environmentResult),
                Is.True,
                Describe(environmentResult));
            GameplayTriggerCompileResult compile = GameplayTriggerConfigCompiler.Compile(
                document,
                GameplayTriggerCompilerOptions.Default,
                environment);
            Assert.That(compile.Succeeded, Is.True, Describe(compile));

            var sink = new RecordingSink(512);
            GameplayTriggerRuntimeCapacityProfile profile = ExactProfile(
                compile.CompiledCatalog.CapacityRequirements);
            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    compile.CompiledCatalog,
                    new[] { GameplayTriggerActionBootstrapBinding.FromSink(sink) },
                    profile);
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            return new TestComposition(
                compile.CompiledCatalog,
                bootstrap.Catalog,
                bootstrap.RuntimeOptions.Value,
                new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value),
                sink);
        }

        private static GameplayTriggerRuntimeCapacityProfile ExactProfile(
            GameplayTriggerRuntimeCapacityRequirements required) =>
            new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

        private static GameplayTriggerRuntime CreateRuntimeWithInputCapacity(
            TestComposition composition,
            int maxInputItems)
        {
            GameplayTriggerRuntimeOptions source = composition.Options;
            var options = new GameplayTriggerRuntimeOptions(
                source.MaxInputBatches,
                maxInputItems,
                source.MaxRelations,
                source.MaxOccurrences,
                source.MaxCandidates,
                source.MaxRouteStates,
                source.MaxActivations,
                source.MaxIntents,
                source.MaxPreparedActions,
                source.MaxConditionStackDepth,
                source.MaxDeferredRecords,
                source.MaxReliableOutboxRecords);
            return new GameplayTriggerRuntime(composition.Catalog, options);
        }

        private static GameplayTriggerCombatSensorSystem CreateSystem(
            TestComposition composition,
            CombatPhysicsWorld physicsWorld,
            ICombatEntityGameplayMapReader reader,
            GameplayComponentWorld componentWorld,
            GameplayTriggerCombatSensorDescriptor[] descriptors)
        {
            GameplayTriggerCombatSensorCatalog catalog = CreateCatalog(composition.Plan, descriptors);
            Assert.That(
                GameplayTriggerCombatSensorSystem.TryCreate(
                    catalog,
                    composition.Runtime,
                    physicsWorld,
                    reader,
                    componentWorld,
                    out GameplayTriggerCombatSensorSystem system,
                    out GameplayTriggerCombatSensorInitializationResult result),
                Is.True,
                Describe(result));
            return system;
        }

        private static GameplayTriggerCombatSensorCatalog CreateCatalog(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerCombatSensorDescriptor[] descriptors)
        {
            Assert.That(
                GameplayTriggerCombatSensorCatalog.TryCreate(
                    descriptors,
                    plan,
                    out GameplayTriggerCombatSensorCatalog catalog,
                    out GameplayTriggerCombatSensorCatalogResult result),
                Is.True,
                Describe(result));
            return catalog;
        }

        private static void AssertCatalogFailure(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerCombatSensorDescriptor[] descriptors,
            GameplayTriggerCombatSensorCatalogErrorCode expected)
        {
            Assert.That(
                GameplayTriggerCombatSensorCatalog.TryCreate(
                    descriptors,
                    plan,
                    out GameplayTriggerCombatSensorCatalog catalog,
                    out GameplayTriggerCombatSensorCatalogResult result),
                Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(result.ErrorCode, Is.EqualTo(expected), Describe(result));
        }

        private static void AssertSensorHashChanged(
            GameplayTriggerCombatSensorCatalog baseline,
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerCombatSensorDescriptor descriptor,
            string field)
        {
            GameplayTriggerCombatSensorCatalog changed = CreateCatalog(plan, new[] { descriptor });
            Assert.That(changed.SensorCatalogHash, Is.Not.EqualTo(baseline.SensorCatalogHash), field);
        }

        private static GameplayTriggerCombatSensorDescriptor Descriptor(
            int sourceBindingId,
            int providerId,
            CombatPhysicsQuery query,
            int maxQueryHits,
            int maxMappedSubjects) =>
            new GameplayTriggerCombatSensorDescriptor(
                SourceBinding(sourceBindingId),
                new GameplayTriggerSourceProviderId(providerId),
                query,
                maxQueryHits,
                maxMappedSubjects);

        private static GameplayTriggerCombatSensorDescriptor Descriptor(
            int sourceBindingId,
            int providerId,
            CombatPhysicsQuery query,
            int maxQueryHits,
            int maxMappedSubjects,
            GameplayComponentTargetFilter subjectFilter) =>
            new GameplayTriggerCombatSensorDescriptor(
                SourceBinding(sourceBindingId),
                new GameplayTriggerSourceProviderId(providerId),
                query,
                maxQueryHits,
                maxMappedSubjects,
                subjectFilter);

        private static GameplayTriggerSourceBindingId SourceBinding(int value) =>
            new GameplayTriggerSourceBindingId(value);

        private static CombatQueryHeader Header(
            int queryId,
            CombatQueryKind kind,
            CombatPhysicsLayerMask? layerMask = null) =>
            new CombatQueryHeader(
                queryId,
                kind,
                CombatEntityId.None,
                traceId: queryId,
                actionId: queryId,
                sourceOrder: queryId,
                layerMask: layerMask ?? CombatPhysicsLayerMask.FromLayer(Layer));

        private static CombatPhysicsQuery SphereQuery(
            int queryId,
            CombatPhysicsLayerMask? layerMask = null) =>
            CombatPhysicsQuery.From(new CombatSphereQuery(
                Header(queryId, CombatQueryKind.Sphere, layerMask),
                FixVector3.Zero,
                Fix64.FromInt(100)));

        private static CombatPhysicsQuery CenteredSphereQuery(
            int queryId,
            int centerX,
            int radius) =>
            CombatPhysicsQuery.From(new CombatSphereQuery(
                Header(queryId, CombatQueryKind.Sphere),
                new FixVector3(Fix64.FromInt(centerX), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(radius)));

        private static CombatPhysicsQuery HashSphereQuery(
            int queryId = 1,
            int sourceEntityId = 0,
            int traceId = 1,
            int actionId = 1,
            int sourceOrder = 1,
            int layer = Layer,
            bool includeSource = false,
            int centerX = 0,
            int radius = 5)
        {
            CombatQueryHeader header = HashHeader(
                queryId,
                CombatQueryKind.Sphere,
                sourceEntityId,
                traceId,
                actionId,
                sourceOrder,
                layer);
            return CombatPhysicsQuery.From(
                new CombatSphereQuery(
                    header,
                    new FixVector3(Fix64.FromInt(centerX), Fix64.Zero, Fix64.Zero),
                    Fix64.FromInt(radius)),
                new CombatPhysicsQueryFilter(includeSource));
        }

        private static CombatPhysicsQuery HashCapsuleQuery(
            int pointAX = -5,
            int pointBX = 5,
            int radius = 5) =>
            CombatPhysicsQuery.From(new CombatCapsuleQuery(
                HashHeader(1, CombatQueryKind.Capsule, 0, 1, 1, 1, Layer),
                new FixVector3(Fix64.FromInt(pointAX), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(pointBX), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(radius)));

        private static CombatPhysicsQuery HashAabbQuery(
            int minX = -5,
            int maxX = 5) =>
            CombatPhysicsQuery.From(new CombatAabbQuery(
                HashHeader(1, CombatQueryKind.Aabb, 0, 1, 1, 1, Layer),
                new FixVector3(Fix64.FromInt(minX), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(maxX), Fix64.One, Fix64.One)));

        private static CombatPhysicsQuery HashSectorQuery(
            int originX = 0,
            int directionY = 0,
            int radius = 5,
            Fix64? minDot = null) =>
            CombatPhysicsQuery.From(new CombatSectorQuery(
                HashHeader(1, CombatQueryKind.Sector, 0, 1, 1, 1, Layer),
                new FixVector3(Fix64.FromInt(originX), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.One, Fix64.FromInt(directionY), Fix64.Zero),
                Fix64.FromInt(radius),
                minDot ?? Fix64.Zero));

        private static CombatQueryHeader HashHeader(
            int queryId,
            CombatQueryKind kind,
            int sourceEntityId,
            int traceId,
            int actionId,
            int sourceOrder,
            int layer) =>
            new CombatQueryHeader(
                queryId,
                kind,
                sourceEntityId == 0 ? CombatEntityId.None : new CombatEntityId(sourceEntityId),
                traceId,
                actionId,
                sourceOrder,
                CombatPhysicsLayerMask.FromLayer(layer));

        private static CombatPhysicsQuery SphereQueryWithInertFilter(int queryId)
        {
            CombatPhysicsQuery query = SphereQuery(queryId);
            return new CombatPhysicsQuery(
                query.Header,
                query.Shape,
                new CombatPhysicsQueryFilter(false, ownerId: 1));
        }

        private static CombatPhysicsQuery CapsuleQuery(int queryId) =>
            CombatPhysicsQuery.From(new CombatCapsuleQuery(
                Header(queryId, CombatQueryKind.Capsule),
                new FixVector3(Fix64.FromInt(-5), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(5)));

        private static CombatPhysicsQuery AabbQuery(int queryId) =>
            CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(queryId, CombatQueryKind.Aabb),
                new FixVector3(Fix64.FromInt(-100), Fix64.FromInt(-100), Fix64.FromInt(-100)),
                new FixVector3(Fix64.FromInt(100), Fix64.FromInt(100), Fix64.FromInt(100))));

        private static CombatPhysicsQuery SectorQuery(int queryId) =>
            CombatPhysicsQuery.From(new CombatSectorQuery(
                Header(queryId, CombatQueryKind.Sector),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(100),
                Fix64.Zero));

        private static CombatPhysicsQuery RayQuery(int queryId) =>
            CombatPhysicsQuery.From(new CombatRayQuery(
                Header(queryId, CombatQueryKind.Ray),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(100)));

        private static CombatPhysicsQuery ObbQuery(int queryId) =>
            CombatPhysicsQuery.From(new CombatObbQuery(
                Header(queryId, CombatQueryKind.Obb),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)));

        private static void AddCollider(
            CombatPhysicsWorld world,
            int combatValue,
            int bodyValue,
            int colliderValue,
            int x)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(combatValue),
                bodyId,
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                Layer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }

        private static void AddAdditionalCollider(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue)
        {
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(bodyValue),
                new CombatColliderId(colliderValue),
                Layer,
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One)));
        }

        private static GameplaySystemContext Context(
            RuntimeFrame frame,
            GameplayComponentWorld componentWorld,
            GameplayWorld gameplayWorld = null,
            GameplayCommandExecutionState commandState = null) =>
            new GameplaySystemContext(
                frame,
                0d,
                0d,
                gameplayWorld ?? new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                componentWorld.Events,
                commandState ?? new GameplayCommandExecutionState(),
                componentWorld);

        private static void AssertAllRows(
            GameplayTriggerCombatSensorSystem system,
            GameplayTriggerCombatSensorTickStatus expected)
        {
            for (int i = 0; i < system.BindingResultCount; i++)
                Assert.That(system.GetLastBindingResult(i).Status, Is.EqualTo(expected));
        }

        private static void AssertSingleOccurrence(
            GameplayTriggerRuntime runtime,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject)
        {
            Assert.That(runtime.OccurrenceCount, Is.EqualTo(1));
            Assert.That(runtime.GetOccurrence(0).Kind, Is.EqualTo(kind));
            Assert.That(runtime.GetOccurrence(0).Subject, Is.EqualTo(subject));
        }

        private static OrderRun RunOrderScenario(
            int firstX,
            int secondX,
            int firstBody,
            int secondBody)
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId first = componentWorld.CreateEntity();
            GameplayEntityId second = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, firstBody, 1, firstX);
            AddAdditionalCollider(physicsWorld, firstBody, 2);
            AddCollider(physicsWorld, 2, secondBody, 1, secondX);
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), first);
            map.Register(new CombatEntityId(2), second);
            GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 3, 4) });
            sensor.Tick(Context(RuntimeFrame.Zero, componentWorld));
            Assert.That(composition.Runtime.Evaluate(RuntimeFrame.Zero).Success, Is.True);
            Assert.That(composition.Runtime.OccurrenceCount, Is.EqualTo(2));
            Assert.That(composition.Sink.Count, Is.EqualTo(2));
            return new OrderRun(
                composition.Runtime.GetOccurrence(0).Subject,
                composition.Runtime.GetOccurrence(1).Subject,
                composition.Sink.GetTarget(0),
                composition.Sink.GetTarget(1),
                ComputeAuthorityHash(composition.Runtime, RuntimeFrame.Zero));
        }

        private static long ComputeAuthorityHash(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(frame),
                accumulator);
            return accumulator.ToHash();
        }

        private static RestoreRun CreateRestoreRun()
        {
            TestComposition composition = CreateComposition();
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId subject = componentWorld.CreateEntity();
            var physicsWorld = new CombatPhysicsWorld();
            AddCollider(physicsWorld, 1, 1, 1, 0);
            var map = new CombatEntityGameplayMap();
            map.Register(new CombatEntityId(1), subject);
            GameplayTriggerCombatSensorSystem sensor = CreateSystem(
                composition,
                physicsWorld,
                map,
                componentWorld,
                new[] { Descriptor(1, 7, SphereQuery(1), 1, 4) });
            return new RestoreRun(
                composition,
                componentWorld,
                sensor,
                new GameplayTriggerEvaluationSystem(composition.Runtime));
        }

        private static void TickAndEvaluate(RestoreRun run, long frame)
        {
            GameplaySystemContext context = Context(new RuntimeFrame(frame), run.ComponentWorld);
            run.Sensor.Tick(context);
            run.Evaluation.Tick(context);
        }

        private static string Describe(GameplayTriggerCombatSensorCatalogResult result) =>
            result.ErrorCode + " @ " + result.DescriptorIndex + " (" + result.Detail0 + "," + result.Detail1 + ")";

        private static string Describe(GameplayTriggerCombatSensorInitializationResult result) =>
            result.ErrorCode + " @ " + result.DescriptorIndex + " (" + result.Expected + "," + result.Actual + ")";

        private static string Describe(GameplayTriggerCompileResult result)
        {
            if (result == null)
                return "null";
            if (result.Succeeded)
                return "success";
            return result.DiagnosticCount > 0
                ? result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path
                : "failed without diagnostic";
        }

        private static string Describe(GameplayTriggerConfigRegistryResult result) =>
            result.Succeeded || result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private static string Describe(GameplayTriggerCatalogBootstrapResult result) =>
            result.Succeeded || result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private sealed class RecordingSink : IGameplayTriggerActionSink
        {
            private readonly GameplayTriggerTargetRef[] _targets;
            private int _nextToken;

            public RecordingSink(int capacity)
            {
                _targets = new GameplayTriggerTargetRef[capacity];
            }

            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerCombatSensorBridgeTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int Count { get; private set; }

            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = ++_nextToken;
                return true;
            }

            public void Abort(int token)
            {
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                if (Count < _targets.Length)
                    _targets[Count] = intent.Target;
                Count++;
            }

            public GameplayTriggerTargetRef GetTarget(int index) => _targets[index];
        }

        private sealed class FixedMapReader : ICombatEntityGameplayMapReader
        {
            private readonly int[] _combatIds;
            private readonly GameplayEntityId[] _gameplayIds;

            public FixedMapReader(int[] combatIds, GameplayEntityId[] gameplayIds)
            {
                _combatIds = combatIds;
                _gameplayIds = gameplayIds;
            }

            public bool TryGetGameplayId(CombatEntityId combatId, out GameplayEntityId gameplayId)
            {
                for (int i = 0; i < _combatIds.Length; i++)
                {
                    if (_combatIds[i] != combatId.Value)
                        continue;
                    gameplayId = _gameplayIds[i];
                    return true;
                }
                gameplayId = default;
                return false;
            }
        }

        private sealed class OneInvalidMapReader : ICombatEntityGameplayMapReader
        {
            private readonly GameplayEntityId _valid;

            public OneInvalidMapReader(GameplayEntityId valid)
            {
                _valid = valid;
            }

            public bool TryGetGameplayId(CombatEntityId combatId, out GameplayEntityId gameplayId)
            {
                gameplayId = combatId.Value == 1 ? default : _valid;
                return true;
            }
        }

        private sealed class WorldValidator : IGameplayTriggerRestoreEntityValidator
        {
            private readonly GameplayComponentWorld _world;

            public WorldValidator(GameplayComponentWorld world)
            {
                _world = world;
            }

            public bool IsRestoredEntityValid(GameplayEntityId entityId) =>
                _world.IsAlive(entityId);
        }

        private readonly struct TestComposition
        {
            public TestComposition(
                GameplayTriggerCompiledCatalogPlan plan,
                GameplayTriggerCatalog catalog,
                GameplayTriggerRuntimeOptions options,
                GameplayTriggerRuntime runtime,
                RecordingSink sink)
            {
                Plan = plan;
                Catalog = catalog;
                Options = options;
                Runtime = runtime;
                Sink = sink;
            }

            public GameplayTriggerCompiledCatalogPlan Plan { get; }
            public GameplayTriggerCatalog Catalog { get; }
            public GameplayTriggerRuntimeOptions Options { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public RecordingSink Sink { get; }
        }

        private readonly struct OrderRun
        {
            public OrderRun(
                GameplayEntityId firstSubject,
                GameplayEntityId secondSubject,
                GameplayTriggerTargetRef firstActionTarget,
                GameplayTriggerTargetRef secondActionTarget,
                long authorityHash)
            {
                FirstSubject = firstSubject;
                SecondSubject = secondSubject;
                FirstActionTarget = firstActionTarget;
                SecondActionTarget = secondActionTarget;
                AuthorityHash = authorityHash;
            }

            public GameplayEntityId FirstSubject { get; }
            public GameplayEntityId SecondSubject { get; }
            public GameplayTriggerTargetRef FirstActionTarget { get; }
            public GameplayTriggerTargetRef SecondActionTarget { get; }
            public long AuthorityHash { get; }
        }

        private readonly struct RestoreRun
        {
            public RestoreRun(
                TestComposition composition,
                GameplayComponentWorld componentWorld,
                GameplayTriggerCombatSensorSystem sensor,
                GameplayTriggerEvaluationSystem evaluation)
            {
                Composition = composition;
                ComponentWorld = componentWorld;
                Sensor = sensor;
                Evaluation = evaluation;
            }

            public TestComposition Composition { get; }
            public GameplayComponentWorld ComponentWorld { get; }
            public GameplayTriggerCombatSensorSystem Sensor { get; }
            public GameplayTriggerEvaluationSystem Evaluation { get; }
        }
    }
}
