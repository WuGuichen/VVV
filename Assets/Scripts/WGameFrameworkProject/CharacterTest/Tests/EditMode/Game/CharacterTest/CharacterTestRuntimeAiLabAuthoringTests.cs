using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabAuthoringTests
    {

        [Test]
        public void CandidateComparison_SummaryIncludesStableAuthorityHash()
        {
            var report = new CharacterTestRuntimeAiLabCandidateComparisonReport(
                1,
                4,
                7,
                "Pursuit Dash",
                0x1234L,
                repeatable: true);

            string summary = report.ToSummary();
            Assert.That(summary, Does.Contain("0000000000001234"));
            Assert.That(summary, Does.Contain("可重复"));
        }

        [TestCase((int)CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, "≤")]
        [TestCase((int)CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual, "≥")]
        [TestCase((int)CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan, ">")]
        [TestCase((int)CharacterTestRuntimeAiLabCardConditionOperator.Equals, "=")]
        [TestCase((int)CharacterTestRuntimeAiLabCardConditionOperator.NotEquals, "≠")]
        public void GameplayProjection_DistanceSummary_UsesActualOperator(
            int conditionOperator,
            string expectedSymbol)
        {
            var conditions = new[]
            {
                new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "distance",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    Operator = (CharacterTestRuntimeAiLabCardConditionOperator)conditionOperator,
                    Value = 1f
                }
            };

            Assert.That(
                CharacterTestRuntimeAiLabGameplayAuthoring.FormatTriggers(conditions),
                Does.Contain(expectedSymbol));
        }

        [Test]
        public void GameplayProjection_DistanceSummary_FormatsRange()
        {
            var conditions = new[]
            {
                new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "distance",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                    Value = 1f,
                    MaxValue = 3f
                }
            };

            Assert.That(
                CharacterTestRuntimeAiLabGameplayAuthoring.FormatTriggers(conditions),
                Does.Contain("1–3 m"));
        }

        [Test]
        public void GameplayProjection_StopDistanceEdit_UpdatesConditionAndMovementStepTogether()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(1.5f);

            Assert.That(CharacterTestRuntimeAiLabGameplayAuthoring.TrySetStopDistance(
                source,
                2.4f,
                out CharacterTestRuntimeAiLabBehaviorCardPayload edited), Is.True);
            Assert.That(CharacterTestRuntimeAiLabGameplayAuthoring.TryGetStopDistance(
                edited,
                out float stopDistance), Is.True);
            Assert.That(stopDistance, Is.EqualTo(2.4f));
            Assert.That(edited.TriggerConditions.Find(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance).Operator,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan));
            Assert.That(edited.Steps[0].MoveMinDistance, Is.EqualTo(2.4f));
            Assert.That(source.Steps[0].MoveMinDistance, Is.EqualTo(1.5f));
            Assert.That(CharacterTestRuntimeAiLabGameplayAuthoring.BuildTriggerSummary(edited),
                Does.Contain("完成 ≤ 2.4 m"));
        }

        [Test]
        public void ContinuousApproachJson_MigratesMatchingLegacyInclusiveBoundaryOnly()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload legacy =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(1.5f);
            CharacterTestRuntimeAiLabCardCondition distance = legacy.TriggerConditions.Find(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            distance.Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual;
            string json = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(legacy);

            CharacterTestRuntimeAiLabBehaviorCardPayload migrated =
                CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(json);

            Assert.That(migrated.TriggerConditions.Find(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance).Operator,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan));
        }

        [Test]
        public void LegacyApproachActivation_MigratesRangeFlagToExplicitDistanceBoundary()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload legacy =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            legacy.TriggerConditions.RemoveAll(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            legacy.TriggerConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.out-of-range",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = false
            });

            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardFactory.ApplyLegacyActivationMigration(
                    legacy,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    1.6f),
                Is.True);
            CharacterTestRuntimeAiLabCardCondition distance = legacy.TriggerConditions.Find(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            Assert.That(distance, Is.Not.Null);
            Assert.That(distance.Operator,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan));
            Assert.That(distance.Value, Is.EqualTo(1.6f));
            Assert.That(legacy.TriggerConditions.Exists(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange),
                Is.False);
        }

        [Test]
        public void GameplayPlan_DistanceMetric_UsesObservedValueAndCompletionBoundary()
        {
            var timeline = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                StageCount = 1,
                ObservedTargetDistanceDecimeters = 35,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        StageId = "approach",
                        Title = "持续靠近",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                        MoveMinDistance = 1.5f,
                        MoveMaxDistance = 999f,
                        OwnsPrimaryAction = true,
                        Current = true
                    }
                }
            };
            var projection = new GameplayReadablePlanProjection();

            projection.Rebuild(timeline);

            GameplayReadablePlanStageProjection stage = projection.GetStage(0);
            Assert.That(stage.MetricKind, Is.EqualTo(GameplayReadablePlanMetricKind.Distance));
            Assert.That(stage.MetricCurrentText, Is.EqualTo("距离 3.5m"));
            Assert.That(stage.MetricTargetText, Is.EqualTo("/ ≤1.5m"));
            Assert.That(stage.TooltipText, Does.Contain("完成条件：距离 ≤1.5m"));
        }

        [TestCase(0f)]
        [TestCase(10.1f)]
        public void GameplayProjection_InvalidStopDistance_DoesNotProducePayload(float value)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(1.5f);

            Assert.That(CharacterTestRuntimeAiLabGameplayAuthoring.TrySetStopDistance(
                source,
                value,
                out CharacterTestRuntimeAiLabBehaviorCardPayload edited), Is.False);
            Assert.That(edited, Is.Null);
            Assert.That(source.Steps[0].MoveMinDistance, Is.EqualTo(1.5f));
        }

        [TestCase(-1, 12)]
        [TestCase(20, 12)]
        [TestCase(6, 241)]
        public void GameplayProjection_InvalidThreatWindow_DoesNotProducePayload(int minimum, int maximum)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic();

            Assert.That(CharacterTestRuntimeAiLabGameplayAuthoring.TrySetThreatWindow(
                source,
                minimum,
                maximum,
                out CharacterTestRuntimeAiLabBehaviorCardPayload edited), Is.False);
            Assert.That(edited, Is.Null);
        }
    }
}
