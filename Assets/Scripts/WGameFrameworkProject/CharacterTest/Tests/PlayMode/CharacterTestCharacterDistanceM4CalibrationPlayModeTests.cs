using System;
using System.Collections;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM4CalibrationPlayModeTests
    {
        private const int Seed = 42004;
        private const int WarmupTicks = 120;
        private const int SampleTicks = 600;
        private const int PerturbationCount = 5;

        [UnityTest]
        public IEnumerator RepresentativeMatrix_ZeroTruncationPeaks_AreCoveredByProductionDefaults()
        {
            yield return null;
            CalibrationFixture[] fixtures = CreateFixtures();
            CalibrationPeak total = default;
            for (int fixtureIndex = 0; fixtureIndex < fixtures.Length; fixtureIndex++)
            {
                CalibrationFixture fixture = fixtures[fixtureIndex];
                string expectedIdentity = string.Empty;
                for (int perturbation = 0; perturbation < PerturbationCount; perturbation++)
                {
                    fixture.ApplyPermutation(Seed, perturbation);
                    var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
                    var selector = new CharacterTestRuntimeAiJointSelector();
                    var source = new CalibrationEvaluationSource();
                    for (int tick = 0; tick < WarmupTicks + SampleTicks; tick++)
                    {
                        source.CompletedFrame = tick;
                        source.ResetCounts();
                        selector.BeginFrame();
                        CharacterTestRuntimeAiJointGenerationDiagnostics generation =
                            generator.Generate(
                                1, 1,
                                fixture.Actors, fixture.Actors.Length,
                                fixture.Actions, fixture.Actions.Length,
                                fixture.Zones, fixture.Zones.Length,
                                new CharacterTestRuntimeAiJointGenerationBudget());
                        CharacterTestRuntimeAiJointSelectionResult result = selector.Select(
                            generator, source, generation,
                            out CharacterTestRuntimeAiJointSelectorDiagnostics diagnostics);
                        Assert.AreEqual(CharacterTestRuntimeAiJointTruncationPhase.None,
                            diagnostics.TruncationPhase, fixture.Name);
                        Assert.IsTrue(result.HasSelection, fixture.Name);
                        string identity = Identity(result.Selection);
                        if (string.IsNullOrEmpty(expectedIdentity))
                            expectedIdentity = identity;
                        Assert.AreEqual(expectedIdentity, identity,
                            fixture.Name + " perturbation=" + perturbation + " tick=" + tick);
                        if (tick >= WarmupTicks)
                        {
                            total.Include(generation, diagnostics, source);
                        }
                    }
                }
            }

            Assert.AreEqual(32, total.ActorCandidates);
            Assert.AreEqual(16, total.ActorTopK);
            Assert.AreEqual(4, total.ReadyActions);
            Assert.AreEqual(256, total.ActionZoneBroadphase);
            Assert.AreEqual(256, total.JointCandidates);
            Assert.AreEqual(256, total.LeafPreviewTraces);
            Assert.AreEqual(256, total.CandidatePosePreviews);
            Assert.AreEqual(256, total.PathQueries);
            Assert.AreEqual(64, total.TracePlanWorkingSet);
            Assert.AreEqual(256, total.ResultWorkingSet);

            CharacterTestRuntimeAiJointGenerationBudget budget =
                new CharacterTestRuntimeAiJointGenerationBudget().Normalize();
            Assert.GreaterOrEqual(budget.ActorScanLimit, total.ActorCandidates * 2);
            Assert.GreaterOrEqual(budget.ActorTopKLimit, total.ActorTopK * 2);
            Assert.GreaterOrEqual(budget.ReadyActionLimit, total.ReadyActions * 2);
            Assert.GreaterOrEqual(budget.ActionZoneBroadphaseLimit,
                total.ActionZoneBroadphase * 2);
            Assert.GreaterOrEqual(budget.JointCandidateLimit, total.JointCandidates * 2);
            Assert.GreaterOrEqual(budget.LeafPreviewTraceLimit,
                total.LeafPreviewTraces * 2);
            Assert.GreaterOrEqual(budget.CandidatePosePreviewLimit,
                total.CandidatePosePreviews * 2);
            Assert.GreaterOrEqual(budget.PathQueryLimit, total.PathQueries * 2);
            Assert.GreaterOrEqual(
                CharacterTestRuntimeAiJointSelectionCache.DefaultTracePlanCapacity,
                total.TracePlanWorkingSet * 2);
            Assert.GreaterOrEqual(
                CharacterTestRuntimeAiJointSelectionCache.DefaultResultCapacity,
                total.ResultWorkingSet * 2);
        }

        private static CalibrationFixture[] CreateFixtures()
        {
            return new[]
            {
                Fixture("ordinary", 5, 4, 2, 1, false),
                Fixture("dragon-boss", 5, 1, 2, 6, false),
                Fixture("multi-ai-16v16", 32, 16, 4, 4, false),
                Fixture("weapon-mix", 5, 4, 2, 1, true),
                Fixture("blocked-unreachable", 9, 8, 2, 2, false),
                Fixture("compatibility-remote", 5, 4, 0, 0, true)
            };
        }

        private static CalibrationFixture Fixture(
            string name,
            int observedActorCount,
            int opponentCount,
            int evaluatorActionCount,
            int zonesPerOpponent,
            bool compatibilityAction)
        {
            var actors = new CharacterTestRuntimeAiJointActorInput[observedActorCount];
            actors[0] = new CharacterTestRuntimeAiJointActorInput(
                1, 1, CharacterTestCharacterLifeState.Alive, true, 0);
            int opponentStart = observedActorCount - opponentCount;
            for (int i = 1; i < observedActorCount; i++)
            {
                bool opponent = i >= opponentStart;
                actors[i] = new CharacterTestRuntimeAiJointActorInput(
                    i + 1,
                    opponent ? 2 : 1,
                    CharacterTestCharacterLifeState.Alive,
                    true,
                    i * 100L);
            }
            int actionCount = evaluatorActionCount + (compatibilityAction ? 1 : 0);
            var actions = new CharacterTestRuntimeAiJointActionInput[actionCount];
            for (int i = 0; i < evaluatorActionCount; i++)
            {
                actions[i] = new CharacterTestRuntimeAiJointActionInput(
                    i + 1, new CharacterActionId(i + 101),
                    CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                    true, true, 1000 - i,
                    new CharacterTestRuntimeAiJointAabb(0, 0, 0, 100, 100, 100));
            }
            if (compatibilityAction)
            {
                actions[actionCount - 1] = new CharacterTestRuntimeAiJointActionInput(
                    actionCount, new CharacterActionId(actionCount + 100),
                    CharacterTestRuntimeAiJointCandidateSource.CompatibilityLegacyRange,
                    true, true, 100,
                    new CharacterTestRuntimeAiJointAabb(0, 0, 0, 100, 100, 100));
            }
            var zones = new CharacterTestRuntimeAiJointZoneInput[
                opponentCount * zonesPerOpponent];
            int zoneIndex = 0;
            for (int actorIndex = opponentStart; actorIndex < observedActorCount; actorIndex++)
            {
                for (int zone = 0; zone < zonesPerOpponent; zone++)
                {
                    zones[zoneIndex++] = new CharacterTestRuntimeAiJointZoneInput(
                        actorIndex + 1,
                        CharacterTestAuthoredContentIds.D400_Id152 + zone,
                        true,
                        true,
                        zonesPerOpponent - zone,
                        new CharacterTestRuntimeAiJointAabb(0, 0, 0, 100, 100, 100));
                }
            }
            return new CalibrationFixture(name, actors, actions, zones);
        }

        private static string Identity(CharacterTestRuntimeAiJointSelectionCandidate value)
        {
            return value.TargetEntityId + "/" + value.ResolvedActionId
                + "/" + value.ZoneId + "/" + value.AnchorId;
        }

        private sealed class CalibrationFixture
        {
            internal CalibrationFixture(
                string name,
                CharacterTestRuntimeAiJointActorInput[] actors,
                CharacterTestRuntimeAiJointActionInput[] actions,
                CharacterTestRuntimeAiJointZoneInput[] zones)
            {
                Name = name;
                Actors = actors;
                Actions = actions;
                Zones = zones;
            }

            internal string Name { get; }
            internal CharacterTestRuntimeAiJointActorInput[] Actors { get; }
            internal CharacterTestRuntimeAiJointActionInput[] Actions { get; }
            internal CharacterTestRuntimeAiJointZoneInput[] Zones { get; }

            internal void ApplyPermutation(int seed, int index)
            {
                Permute(Actors, seed + index * 17);
                Permute(Actions, seed + index * 31);
                Permute(Zones, seed + index * 47);
            }

            private static void Permute<T>(T[] values, int seed)
            {
                var random = new Random(seed);
                for (int i = values.Length - 1; i > 0; i--)
                {
                    int swap = random.Next(i + 1);
                    T value = values[i];
                    values[i] = values[swap];
                    values[swap] = value;
                }
            }
        }

        private struct CalibrationPeak
        {
            internal int ActorCandidates;
            internal int ActorTopK;
            internal int ReadyActions;
            internal int ActionZoneBroadphase;
            internal int JointCandidates;
            internal int LeafPreviewTraces;
            internal int CandidatePosePreviews;
            internal int PathQueries;
            internal int TracePlanWorkingSet;
            internal int ResultWorkingSet;

            internal void Include(
                CharacterTestRuntimeAiJointGenerationDiagnostics generation,
                CharacterTestRuntimeAiJointSelectorDiagnostics selection,
                CalibrationEvaluationSource source)
            {
                ActorCandidates = Math.Max(ActorCandidates, generation.ObservedActorCount);
                ActorTopK = Math.Max(ActorTopK, generation.RetainedActorCount);
                ReadyActions = Math.Max(ReadyActions, generation.ReadyActionCount);
                ActionZoneBroadphase = Math.Max(
                    ActionZoneBroadphase, generation.ActionZoneBroadphaseCount);
                JointCandidates = Math.Max(JointCandidates, generation.CandidateCount);
                LeafPreviewTraces = Math.Max(LeafPreviewTraces, selection.EvaluationCount);
                CandidatePosePreviews = Math.Max(
                    CandidatePosePreviews, source.CandidatePosePreviewCount);
                PathQueries = Math.Max(PathQueries, source.PathQueryCount);
                TracePlanWorkingSet = Math.Max(TracePlanWorkingSet, source.TracePlanKeyCount);
                ResultWorkingSet = Math.Max(ResultWorkingSet, generation.CandidateCount);
            }
        }

        private sealed class CalibrationEvaluationSource :
            ICharacterTestRuntimeAiJointEvaluationSource
        {
            internal int CompletedFrame { get; set; }
            internal int CandidatePosePreviewCount { get; private set; }
            internal int PathQueryCount { get; private set; }
            internal int TracePlanKeyCount { get; private set; }

            internal void ResetCounts()
            {
                CandidatePosePreviewCount = 0;
                PathQueryCount = 0;
                TracePlanKeyCount = 0;
            }

            public bool TryCreateCacheKeys(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                out CharacterTestRuntimeAiJointTracePlanCacheKey tracePlanKey,
                out CharacterTestRuntimeAiJointResultCacheKey resultKey)
            {
                tracePlanKey = new CharacterTestRuntimeAiJointTracePlanCacheKey(
                    1, seed.Action.ResolvedActionId,
                    CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                    0, 0, 0, 0, CompletedFrame, 1, 1, 1,
                    seed.Action.ActionBindingRevision,
                    seed.Action.AnimationRevision,
                    seed.Action.RootMotionRevision,
                    seed.Action.TracePolicyRevision,
                    seed.TargetEntityId);
                resultKey = new CharacterTestRuntimeAiJointResultCacheKey(
                    tracePlanKey, seed.TargetEntityId, 1, 1,
                    CharacterTestAttackEvaluationMode.CandidatePose,
                    seed.ZoneId, CharacterTestAuthoredContentIds.D400_Id158, 0, 0, 0, 0,
                    1, 1, 0, 1, 1, CompletedFrame, 1);
                return true;
            }

            public bool TryBuildTracePlan(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                out CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan)
            {
                TracePlanKeyCount++;
                tracePlan = new CharacterTestRuntimeAiJointTracePlanCacheValue(default, 1, true);
                return true;
            }

            public bool TryEvaluate(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan,
                out CharacterTestRuntimeAiJointEvaluation evaluation)
            {
                CandidatePosePreviewCount++;
                PathQueryCount++;
                evaluation = new CharacterTestRuntimeAiJointEvaluation(
                    CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                    true, false, true,
                    seed.TargetEntityId * 100,
                    seed.TargetEntityId,
                    CharacterTestAuthoredContentIds.D400_Id158);
                return true;
            }
        }
    }
}
