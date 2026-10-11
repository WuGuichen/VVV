using System;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM2AuthorityStateTests
    {
        [Test]
        public void TargetDistanceUnavailable_FailsClosedWhileRootDistanceRemainsQueryable()
        {
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetTarget(
                targetEntityId: 2,
                targetDistance: 0f,
                targetDistanceAvailable: false,
                targetRootDistance: 12f,
                navigation: new CharacterTestAiNavigationResult(
                    UnityEngine.Vector2.zero,
                    directPathClear: true,
                    targetInAttackRange: false,
                    selectedCandidateIndex: -1),
                selfActionRunning: false,
                targetVisible: true,
                targetLineOfSightBlocked: false,
                targetLastSeenAgeFrames: 0,
                targetThreatScore: 0f);
            var distanceCondition = new CharacterTestRuntimeAiPlannerConditionDefinition(
                CharacterTestRuntimeAiPlannerFactKind.TargetDistance,
                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual,
                boolValue: false,
                floatValue: 100f);
            var rootCondition = new CharacterTestRuntimeAiPlannerConditionDefinition(
                CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance,
                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual,
                boolValue: false,
                floatValue: 20f);

            Assert.IsFalse(CharacterTestRuntimeAiPlannerConditionEvaluator.IsSatisfied(
                facts, distanceCondition));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerConditionEvaluator.IsSatisfied(
                facts, rootCondition));
            Assert.IsFalse(facts.TargetDistanceAvailable);
            Assert.AreEqual(12f, facts.TargetRootDistance);
        }

        [Test]
        public void Snapshot_ClonePreservesAllLockFieldsAndIsIndependent()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot source = CreateState();
            CharacterTestRuntimeAiPlannerStateSnapshot clone = source.CloneForRollback();
            CharacterTestDamageZoneLockStateSnapshot actual = clone.DamageZoneLocks[0];
            Assert.AreEqual(7, actual.SourceEntityId);
            Assert.AreEqual(11, actual.TargetGameplayEntityIndex);
            Assert.AreEqual(3, actual.TargetGameplayEntityGeneration);
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07, actual.ZoneId);
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry10, actual.HitZoneId);
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry11, actual.LogicalHurtboxId);
            Assert.AreEqual(19L, actual.SelectedFrame);
            source.DamageZoneLocks[0].ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry12;
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07, actual.ZoneId);
        }

        [Test]
        public void StateHash_IsSensitiveToStableActorAndSubtargetIdentity()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot baseline = CreateState();
            long baselineHash = Hash(baseline);
            Action<CharacterTestDamageZoneLockStateSnapshot>[] mutations =
            {
                value => value.SourceEntityId++,
                value => value.TargetGameplayEntityIndex++,
                value => value.TargetGameplayEntityGeneration++,
                value => value.TargetCombatEntityId++,
                value => value.ZoneId++,
                value => value.HitZoneId++,
                value => value.LogicalHurtboxId++
            };
            for (int i = 0; i < mutations.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStateSnapshot candidate = baseline.CloneForRollback();
                mutations[i](candidate.DamageZoneLocks[0]);
                Assert.AreNotEqual(baselineHash, Hash(candidate), "field index=" + i);
            }
        }

        [Test]
        public void StateHash_IgnoresDerivedActionOpportunityAndGeometryEvidence()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot baseline = CreateState();
            CharacterTestRuntimeAiPlannerStateSnapshot rebuilt = baseline.CloneForRollback();
            rebuilt.DamageZoneLocks[0].HurtboxRevision++;
            rebuilt.DamageZoneLocks[0].WorldRevision++;
            rebuilt.DamageZoneLocks[0].ResolvedActionRevision++;
            rebuilt.DamageZoneLocks[0].OpportunityLevel--;
            rebuilt.DamageZoneLocks[0].EarliestHitTick++;
            rebuilt.DamageZoneLocks[0].BindingRevision++;
            rebuilt.DamageZoneLocks[0].ZoneRevision++;
            rebuilt.DamageZoneLocks[0].BodyPartStateRevision++;
            rebuilt.DamageZoneLocks[0].SelectedFrame++;
            rebuilt.DamageZoneLocks[0].SwitchReason++;

            Assert.AreEqual(Hash(baseline), Hash(rebuilt));
        }

        [Test]
        public void Adapter_RestoreCaptureRoundTripIsCanonicalAndSorted()
        {
            var adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            CharacterTestDamageZoneLockStateSnapshot[] values =
            {
                CreateLock(9, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04),
                CreateLock(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07)
            };
            Assert.IsTrue(adapter.TryRestoreZoneLocks(values, out string failure), failure);
            CharacterTestDamageZoneLockStateSnapshot[] captured = adapter.CaptureZoneLocks();
            Assert.AreEqual(2, captured.Length);
            Assert.AreEqual(2, captured[0].SourceEntityId);
            Assert.AreEqual(9, captured[1].SourceEntityId);
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07, captured[0].ZoneId);
        }

        [Test]
        public void Adapter_InvalidOrDuplicatePayloadFailsClosedAndClearsState()
        {
            var adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            CharacterTestDamageZoneLockStateSnapshot valid = CreateLock(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07);
            Assert.IsFalse(adapter.TryRestoreZoneLocks(
                new[] { valid, CreateLock(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04) }, out _));
            Assert.AreEqual(0, adapter.CaptureZoneLocks().Length);
            valid.LogicalHurtboxId = 0;
            Assert.IsFalse(adapter.TryRestoreZoneLocks(new[] { valid }, out _));
            Assert.AreEqual(0, adapter.CaptureZoneLocks().Length);
        }

        [Test]
        public void Adapter_HurtboxOnlyLockAllowsEmptyOptionalZone()
        {
            var adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            CharacterTestDamageZoneLockStateSnapshot hurtboxOnly =
                CreateLock(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07);
            hurtboxOnly.ZoneId = 0;

            Assert.IsTrue(adapter.TryRestoreZoneLocks(new[] { hurtboxOnly }, out string failure), failure);
            CharacterTestDamageZoneLockStateSnapshot[] captured =
                adapter.CaptureZoneLocks();
            Assert.AreEqual(1, captured.Length);
            Assert.AreEqual(0, captured[0].ZoneId);
            Assert.Greater(captured[0].LogicalHurtboxId, 0);
        }

        [Test]
        public void EmptyLegacyNormalizationHasNoLockAuthority()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot empty = CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            Assert.AreEqual(CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion, empty.SchemaVersion);
            Assert.IsNotNull(empty.DamageZoneLocks);
            Assert.AreEqual(0, empty.DamageZoneLocks.Length);
        }

        [Test]
        public void OwnershipHashes_IncludeExactlyOwnedOrNonOwnedLocks()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot baseline = CreateState();
            baseline.DamageZoneLocks = new[] { CreateLock(7, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07), CreateLock(9, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04) };
            CharacterTestRuntimeAiPlannerStateSnapshot changedOwned = baseline.CloneForRollback();
            changedOwned.DamageZoneLocks[0].ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry05;
            CharacterTestRuntimeAiPlannerStateSnapshot changedOther = baseline.CloneForRollback();
            changedOther.DamageZoneLocks[1].ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry05;

            Assert.AreNotEqual(OwnershipHash(baseline, 7, true), OwnershipHash(changedOwned, 7, true));
            Assert.AreEqual(OwnershipHash(baseline, 7, true), OwnershipHash(changedOther, 7, true));
            Assert.AreEqual(OwnershipHash(baseline, 7, false), OwnershipHash(changedOwned, 7, false));
            Assert.AreNotEqual(OwnershipHash(baseline, 7, false), OwnershipHash(changedOther, 7, false));
        }

        private static CharacterTestRuntimeAiPlannerStateSnapshot CreateState()
        {
            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>(),
                BehaviorLogicSlots = Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>(),
                DamageZoneLocks = new[] { CreateLock(7, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry07) }
            };
        }

        private static CharacterTestDamageZoneLockStateSnapshot CreateLock(int sourceId, int zoneId)
        {
            return new CharacterTestDamageZoneLockStateSnapshot
            {
                SourceEntityId = sourceId,
                TargetGameplayEntityIndex = 11,
                TargetGameplayEntityGeneration = 3,
                TargetCombatEntityId = 13,
                ResolvedActionRevision = 17,
                ZoneId = zoneId,
                HitZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry10,
                LogicalHurtboxId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry11,
                OpportunityLevel = (int)CharacterTestAttackOpportunityLevel.CurrentPose,
                EarliestHitTick = 4,
                BindingRevision = 101,
                HurtboxRevision = 103,
                WorldRevision = 107,
                ZoneRevision = 109,
                SelectedFrame = 19,
                SwitchReason = (int)CharacterTestDamageZoneSwitchReason.InitialSelection
            };
        }

        private static long Hash(CharacterTestRuntimeAiPlannerStateSnapshot state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long OwnershipHash(
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            int ownedEntityId,
            bool includeOwned)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHashForOwnership(
                accumulator, state, ownedEntityId, includeOwned);
            return accumulator.ToHash();
        }
    }
}
