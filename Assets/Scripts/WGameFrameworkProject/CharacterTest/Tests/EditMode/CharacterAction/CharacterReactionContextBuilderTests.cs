using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterReactionContextBuilderTests
    {
        [Test]
        public void CohesionBreak_BuildsPressureOnlyContextWithIncompleteHitDiagnostics()
        {
            CharacterReactionContextBuildResult result = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(10),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20,
                    sourceId: 7,
                    reason: 1,
                    traceId: CharacterTestRuntimeTraceIds.Pb1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, result.Completeness);
            Assert.AreEqual(CharacterReactionContextSourceKind.CohesionBreak, result.Context.SourceKind);
            Assert.AreEqual(Entity(), result.Context.EntityId);
            Assert.AreEqual(PressureBand.Critical, result.Context.PreviousPressureBand);
            Assert.AreEqual(PressureBand.Broken, result.Context.CurrentScatterPressureBand);
            Assert.AreEqual(100, result.Context.CurrentScatterPressure);
            Assert.AreEqual(7, result.Context.SourceId);
            Assert.AreEqual("scatter-pressure-overflow", result.Context.Reason);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.Pb1, result.Context.TraceId);
            AssertHitFieldsAreEmpty(result.Context);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
        }

        [Test]
        public void GuardArmorAndBandChanged_KeepPressureOnlyContract()
        {
            CharacterReactionContextBuildResult guard = CharacterReactionContextBuilder.FromGuardBreak(
                new GuardBreakEvent(
                    new RuntimeFrame(1),
                    Entity(),
                    PressureBand.Cracked,
                    previousValue: 50,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 50,
                    traceId: CharacterTestRuntimeTraceIds.Guard));
            CharacterReactionContextBuildResult armor = CharacterReactionContextBuilder.FromArmorBreak(
                new ArmorBreakEvent(
                    new RuntimeFrame(2),
                    Entity(),
                    previousIntegrity: 12,
                    currentIntegrity: 0,
                    maxIntegrity: 12,
                    incomingDamage: 18,
                    traceId: CharacterTestRuntimeTraceIds.Armor));
            CharacterReactionContextBuildResult band = CharacterReactionContextBuilder.FromPressureBandChanged(
                new PressureBandChangedEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Pressed,
                    PressureBand.Critical,
                    previousValue: 30,
                    newValue: 80,
                    delta: 50,
                    sourceId: 4,
                    reason: 2,
                    traceId: CharacterTestRuntimeTraceIds.Band));

            Assert.AreEqual(CharacterReactionContextSourceKind.GuardBreak, guard.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, guard.Completeness);
            Assert.AreEqual(PressureBand.Broken, guard.Context.CurrentScatterPressureBand);
            AssertHitFieldsAreEmpty(guard.Context);

            Assert.AreEqual(CharacterReactionContextSourceKind.ArmorBreak, armor.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, armor.Completeness);
            Assert.AreEqual(12, armor.Context.PreviousPressure);
            Assert.AreEqual(0, armor.Context.CurrentScatterPressure);
            Assert.AreEqual(-12, armor.Context.Delta);
            AssertHitFieldsAreEmpty(armor.Context);

            Assert.AreEqual(CharacterReactionContextSourceKind.PressureBandChanged, band.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, band.Completeness);
            Assert.AreEqual(PressureBand.Pressed, band.Context.PreviousPressureBand);
            Assert.AreEqual(PressureBand.Critical, band.Context.CurrentScatterPressureBand);
            Assert.AreEqual(4, band.Context.SourceId);
            AssertHitFieldsAreEmpty(band.Context);
        }

        [Test]
        public void ExplicitDeath_BuildsPressureOnlyContextAndLifecycleBuildsSourceOnlyContext()
        {
            CharacterReactionContextBuildResult death = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(20),
                Entity(),
                reason: "hp-zero",
                traceId: CharacterTestRuntimeTraceIds.Death);
            CharacterReactionContextBuildResult lifecycle = CharacterReactionContextBuilder.FromLifecycle(
                new RuntimeFrame(21),
                Entity(),
                lifecycleState: "PendingDestroy",
                reason: "cleanup",
                traceId: CharacterTestRuntimeTraceIds.Life);

            Assert.AreEqual(CharacterReactionContextSourceKind.Death, death.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, death.Completeness);
            Assert.IsTrue(death.Context.IsDeath);
            Assert.AreEqual("Death", death.Context.LifecycleState);
            AssertDiagnostic(death.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);

            Assert.AreEqual(CharacterReactionContextSourceKind.Lifecycle, lifecycle.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, lifecycle.Completeness);
            Assert.IsFalse(lifecycle.Context.IsDeath);
            Assert.AreEqual("PendingDestroy", lifecycle.Context.LifecycleState);
            AssertDiagnostic(lifecycle.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
        }

        [Test]
        public void ReactionSelector_SelectsCohesionBreakReactAndDeathUnderPressureOnlyScope()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_Death, CharacterReactionRuleTrigger.Death),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakReact, CharacterReactionRuleTrigger.CohesionBreak)
                });
            CharacterReactionContext scatterPressure = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 10)).Context;
            CharacterReactionContext death = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(9),
                Entity()).Context;

            CharacterReactionSelectionResult scatterPressureResult = CharacterReactionSelector.Select(profile, scatterPressure);
            CharacterReactionSelectionResult deathResult = CharacterReactionSelector.Select(profile, death);

            Assert.IsTrue(scatterPressureResult.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_CohesionBreakReact, scatterPressureResult.SelectedActionId);
            AssertDiagnostic(scatterPressureResult.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            Assert.IsTrue(deathResult.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_Death, deathResult.SelectedActionId);
            AssertDiagnostic(deathResult.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            AssertDiagnostic(deathResult.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
        }

        [Test]
        public void ReactionSelector_UsesSpecificityPriorityOrderAndStableDiagnostics()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_AnyCriticalReact, CharacterReactionRuleTrigger.Any, currentScatterPressureBand: PressureBand.Broken, priority: 100),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakReact, CharacterReactionRuleTrigger.CohesionBreak, priority: 1),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakHighPriority, CharacterReactionRuleTrigger.CohesionBreak, priority: 10),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakLaterTie, CharacterReactionRuleTrigger.CohesionBreak, priority: 10),
                });
            CharacterReactionContext scatterPressure = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 10)).Context;

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, scatterPressure);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_CohesionBreakHighPriority, result.SelectedActionId);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
        }

        [Test]
        public void ReactionSelector_FallsBackToDefaultWhenNoRuleMatches()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakReact, CharacterReactionRuleTrigger.CohesionBreak),
                },
                defaultActionId: CharacterTestActionIds.Action_LightHitReact);
            CharacterReactionContext context = CharacterReactionContextBuilder.FromPressureBandChanged(
                new PressureBandChangedEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Stable,
                    PressureBand.Pressed,
                    previousValue: 0,
                    newValue: 25,
                    delta: 25)).Context;

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, context);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_LightHitReact, result.SelectedActionId);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionFallbackUsed);
        }

        [Test]
        public void HitContextRule_ReturnsMissingFieldErrorUnderPressureOnlyContext()
        {
            var profile = new CharacterReactionProfile(
                "directional",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_FrontBodyHitReact,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        requiresBodyPart: true,
                        requiresHitZone: true,
                        requiresHitDirection: true)
                });
            CharacterReactionContext context = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 10)).Context;

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, context);
            CharacterActionDiagnostic[] validatorDiagnostics = CharacterReactionRuleValidator.ValidateAgainstContext(
                profile.Rules[0],
                context);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionContextIncomplete, result.RejectCode);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
            AssertDiagnostic(validatorDiagnostics, CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext);
        }

        [Test]
        public void HitSource_BuildsFullContextWithAllHitDimensions()
        {
            CharacterReactionContextBuildResult result = CharacterReactionContextBuilder.FromHitSource(
                CreateFullHitSource(
                    bodyPartId: CharacterTestNumericIds.BodyPartHead,
                    hitZoneId: CharacterTestNumericIds.HitZoneFace,
                    damageTypeId: CharacterTestNumericIds.DamageBlunt,
                    hitDirection: CharacterHitDirection.Front,
                    impactForce: 72,
                    reactionGroupId: CharacterTestNumericIds.ReactionUpper));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.Full, result.Completeness);
            Assert.AreEqual(CharacterReactionContextSourceKind.Hit, result.Context.SourceKind);
            Assert.IsTrue(result.Context.HasFullHitContext);
            Assert.AreEqual(CharacterTestNumericIds.BodyPartHead, result.Context.BodyPartId);
            Assert.AreEqual(CharacterTestNumericIds.HitZoneFace, result.Context.HitZoneId);
            Assert.AreEqual(CharacterTestNumericIds.DamageBlunt, result.Context.DamageTypeId);
            Assert.AreEqual(CharacterHitDirection.Front, result.Context.HitDirection);
            Assert.AreEqual(72, result.Context.ImpactForce);
            Assert.AreEqual(CharacterTestNumericIds.ReactionUpper, result.Context.ReactionGroupId);
            Assert.AreEqual(0, result.Diagnostics.Length);
        }

        [Test]
        public void HitSourceNoAlloc_IncompleteHitDoesNotAllocateAfterWarmup()
        {
            var source = new CharacterReactionHitSource(
                new RuntimeFrame(12),
                Entity(),
                bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                hitZoneId: CharacterTestNumericIds.HitZoneRib,
                damageTypeId: CharacterTestNumericIds.DamageSlash,
                hitDirection: CharacterHitDirection.Unknown,
                impactForce: 64,
                reactionGroupId: CharacterTestNumericIds.ReactionCore,
                traceId: CharacterTestRuntimeTraceIds.TraceSourceOnly);

            for (int i = 0; i < 8; i++)
            {
                CharacterReactionContextBuildResult warm =
                    CharacterReactionContextBuilder.FromHitSourceNoAlloc(source);
                Assert.IsTrue(warm.Success);
                Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, warm.Completeness);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CharacterReactionContextBuildResult result = default;
            for (int i = 0; i < 128; i++)
                result = CharacterReactionContextBuilder.FromHitSourceNoAlloc(source);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, result.Completeness);
            Assert.AreEqual(0, result.Diagnostics.Length);
        }

        [Test]
        public void ReactionSelector_FullHitContext_SelectsByEveryHitDimension()
        {
            CharacterReactionContext context = CharacterReactionContextBuilder.FromHitSource(
                CreateFullHitSource(
                    bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                    hitZoneId: CharacterTestNumericIds.HitZoneRib,
                    damageTypeId: CharacterTestNumericIds.DamageSlash,
                    hitDirection: CharacterHitDirection.Back,
                    impactForce: 80,
                    reactionGroupId: CharacterTestNumericIds.ReactionCore)).Context;

            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_BodyPartReact, CharacterReactionRuleTrigger.Hit, bodyPartId: CharacterTestNumericIds.BodyPartTorso),
                "BodyPartReact");
            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_HitZoneReact, CharacterReactionRuleTrigger.Hit, hitZoneId: CharacterTestNumericIds.HitZoneRib),
                "HitZoneReact");
            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_DamageTypeReact, CharacterReactionRuleTrigger.Hit, damageTypeId: CharacterTestNumericIds.DamageSlash),
                "DamageTypeReact");
            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_DirectionReact, CharacterReactionRuleTrigger.Hit, hitDirection: CharacterHitDirection.Back),
                "DirectionReact");
            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_ImpactForceReact, CharacterReactionRuleTrigger.Hit, minImpactForce: 60),
                "ImpactForceReact");
            AssertFullHitSelection(
                context,
                new CharacterReactionRule(CharacterTestActionIds.Action_ReactionGroupReact, CharacterReactionRuleTrigger.Hit, reactionGroupId: CharacterTestNumericIds.ReactionCore),
                "ReactionGroupReact");
        }

        [Test]
        public void MissingHitSourceFields_ProduceIncompleteDiagnosticsAndDoNotFallback()
        {
            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                    hitZoneId: CharacterTestNumericIds.HitZoneRib,
                    damageTypeId: 0,
                    hitDirection: CharacterHitDirection.Back,
                    impactForce: null,
                    reactionGroupId: CharacterTestNumericIds.ReactionCore,
                    traceId: CharacterTestRuntimeTraceIds.HitIncomplete));
            var profile = new CharacterReactionProfile(
                "full-hit",
                new CharacterReactionRule[0],
                defaultActionId: CharacterTestActionIds.Action_WrongFallback);

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, build.Context);

            Assert.IsTrue(build.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, build.Completeness);
            Assert.IsFalse(build.Context.HasFullHitContext);
            AssertDiagnostic(build.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
            Assert.IsFalse(selection.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionContextIncomplete, selection.RejectCode);
            Assert.AreEqual(CharacterActionId.None, selection.SelectedActionId);
        }

        [Test]
        public void IncompleteHitSource_SelectsPressureOnlyBrokenHitRule()
        {
            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    traceId: CharacterTestRuntimeTraceIds.BrokenHit,
                    currentScatterPressureBand: PressureBand.Broken,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));
            var profile = new CharacterReactionProfile(
                "broken-hit",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_LightHit, CharacterReactionRuleTrigger.Hit, priority: 100),
                    new CharacterReactionRule(CharacterTestActionIds.Action_BrokenHit, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, priority: 10),
                    new CharacterReactionRule(CharacterTestActionIds.Action_HeadBrokenHit, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, bodyPartId: CharacterTestNumericIds.BodyPartHead, priority: 1000),
                },
                defaultActionId: CharacterTestActionIds.Action_FallbackReact);

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, build.Context);

            Assert.IsTrue(build.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, build.Completeness);
            Assert.IsTrue(selection.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_BrokenHit, selection.SelectedActionId);
            AssertDiagnostic(selection.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            AssertDiagnostic(selection.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
        }

        [Test]
        public void SourceOnlyHitContext_SelectsReactionGroupAndImpactRuleWithoutDirection()
        {
            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    bodyPartId: CharacterTestNumericIds.BodyPartHead,
                    hitZoneId: CharacterTestNumericIds.HitZoneHead,
                    damageTypeId: CharacterTestNumericIds.Slash,
                    hitDirection: CharacterHitDirection.Unknown,
                    impactForce: 80,
                    reactionGroupId: CharacterTestNumericIds.ReactionHeightHigh,
                    traceId: CharacterTestRuntimeTraceIds.BrokenHit,
                    currentScatterPressureBand: PressureBand.Broken,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));
            var profile = new CharacterReactionProfile(
                "broken-hit",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_HighNormal, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, reactionGroupId: CharacterTestNumericIds.ReactionHeightHigh, maxImpactForce: 59, priority: 90),
                    new CharacterReactionRule(CharacterTestActionIds.Action_HighStrong, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, reactionGroupId: CharacterTestNumericIds.ReactionHeightHigh, minImpactForce: 60, priority: 110),
                });

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, build.Context);

            Assert.IsTrue(build.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, build.Completeness);
            Assert.IsFalse(build.Context.HasFullHitContext);
            Assert.IsTrue(selection.Accepted);
            Assert.AreEqual(CharacterTestActionIds.Action_HighStrong, selection.SelectedActionId);
            AssertDiagnostic(selection.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
        }

        [Test]
        public void SourceOnlyHitContext_SplitsImpactForceAtConfiguredThreshold()
        {
            var profile = new CharacterReactionProfile(
                "broken-hit",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_MidNormal, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, reactionGroupId: CharacterTestNumericIds.ReactionHeightMid, maxImpactForce: 59, priority: 90),
                    new CharacterReactionRule(CharacterTestActionIds.Action_MidStrong, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, reactionGroupId: CharacterTestNumericIds.ReactionHeightMid, minImpactForce: 60, priority: 110),
                });

            AssertSelectedImpactRule(profile, 59, "MidNormal");
            AssertSelectedImpactRule(profile, 60, "MidStrong");
        }

        [Test]
        public void IncompleteHitSource_RejectsRequiredHitRuleWhenNoPressureOnlyCandidateMatches()
        {
            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    traceId: CharacterTestRuntimeTraceIds.BrokenHit,
                    currentScatterPressureBand: PressureBand.Broken,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));
            var profile = new CharacterReactionProfile(
                "broken-hit",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_HeadBrokenHit, CharacterReactionRuleTrigger.Hit, currentScatterPressureBand: PressureBand.Broken, bodyPartId: CharacterTestNumericIds.BodyPartHead),
                },
                defaultActionId: CharacterTestActionIds.Action_FallbackReact);

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, build.Context);

            Assert.IsFalse(selection.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext, selection.RejectCode);
            Assert.AreEqual(CharacterActionId.None, selection.SelectedActionId);
            AssertDiagnostic(selection.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext);
            AssertDiagnostic(selection.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
        }

        [Test]
        public void HitResolveResultBridge_ConsumesCombatFactsWithoutDuplicatingAuthority()
        {
            var hitResult = new HitResolveResult(
                new CombatEntityId(10),
                new CombatEntityId(20),
                actionId: 1001,
                actionInstanceId: 33,
                traceId: 44,
                frame: new CombatFrame(7),
                kind: HitResolveKind.Damage,
                damage: 12,
                staggerFrames: 3,
                knockback: new FixVector3(Fix64.FromInt(1), Fix64.Zero, Fix64.Zero));
            CharacterReactionHitSource source = CharacterReactionHitSource.FromHitResolveResult(
                hitResult,
                Entity(),
                bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                hitZoneId: CharacterTestNumericIds.HitZoneRib,
                damageTypeId: CharacterTestNumericIds.DamagePierce,
                hitDirection: CharacterHitDirection.Left,
                impactForce: 12,
                reactionGroupId: CharacterTestNumericIds.ReactionCore);

            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(source);

            Assert.AreEqual(new RuntimeFrame(7), build.Context.Frame);
            Assert.AreEqual(10, build.Context.SourceId);
            Assert.AreEqual("Damage", build.Context.Reason);
            Assert.AreEqual(new RuntimeTraceIdentity(1004, 10, 44, 0), build.Context.TraceId);
            Assert.AreEqual(CharacterReactionContextCompleteness.Full, build.Completeness);
            Assert.IsTrue(hitResult.IsAcceptedDamage);
            Assert.AreEqual(12, hitResult.Damage);
            Assert.AreEqual(HitResolveKind.Damage, hitResult.Kind);
        }

        private static GameplayEntityId Entity()
        {
            return new GameplayEntityId(1, 1);
        }

        private static void AssertSelectedImpactRule(
            CharacterReactionProfile profile,
            int impactForce,
            string expectedActionId)
        {
            CharacterReactionContextBuildResult build = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                    hitZoneId: CharacterTestNumericIds.HitZoneTorso,
                    damageTypeId: CharacterTestNumericIds.Slash,
                    hitDirection: CharacterHitDirection.Unknown,
                    impactForce: impactForce,
                    reactionGroupId: CharacterTestNumericIds.ReactionHeightMid,
                    traceId: CharacterTestRuntimeTraceIds.BrokenHit,
                    currentScatterPressureBand: PressureBand.Broken,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100));

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, build.Context);

            Assert.AreEqual(CharacterReactionContextCompleteness.SourceOnly, build.Completeness);
            Assert.IsTrue(selection.Accepted);
            Assert.AreEqual(expectedActionId, selection.SelectedActionId);
        }

        private static CharacterReactionHitSource CreateFullHitSource(
            int bodyPartId,
            int hitZoneId,
            int damageTypeId,
            CharacterHitDirection hitDirection,
            int impactForce,
            int reactionGroupId)
        {
            return new CharacterReactionHitSource(
                new RuntimeFrame(11),
                Entity(),
                bodyPartId,
                hitZoneId,
                damageTypeId,
                hitDirection,
                impactForce,
                reactionGroupId,
                sourceId: 9,
                reason: "hit",
                traceId: CharacterTestRuntimeTraceIds.HitFull);
        }

        private static void AssertFullHitSelection(
            CharacterReactionContext context,
            CharacterReactionRule matchingRule,
            string expectedActionId)
        {
            var profile = new CharacterReactionProfile(
                "full-hit",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_GenericHitReact, CharacterReactionRuleTrigger.Hit),
                    new CharacterReactionRule(CharacterTestActionIds.Action_WrongBodyReact, CharacterReactionRuleTrigger.Hit, bodyPartId: CharacterTestNumericIds.BodyPartArm, priority: 1000),
                    matchingRule,
                },
                defaultActionId: CharacterTestActionIds.Action_FallbackReact);

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, context);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(expectedActionId, result.SelectedActionId);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
        }

        private static void AssertHitFieldsAreEmpty(CharacterReactionContext context)
        {
            Assert.AreEqual(string.Empty, context.BodyPartId);
            Assert.AreEqual(string.Empty, context.HitZoneId);
            Assert.AreEqual(string.Empty, context.DamageTypeId);
            Assert.AreEqual(CharacterHitDirection.Unknown, context.HitDirection);
            Assert.AreEqual(string.Empty, context.ReactionGroupId);
            Assert.AreEqual(0, context.ImpactForce);
            Assert.IsFalse(context.IsAirborne);
            Assert.AreEqual(string.Empty, context.CurrentActionId);
            Assert.AreEqual(CharacterActionPhaseKind.None, context.CurrentCharacterPhase);
            Assert.IsFalse(context.CurrentActionCommitted);
            Assert.IsTrue(context.CurrentActionInterruptible);
            Assert.IsFalse(context.HasFullHitContext);
        }

        private static void AssertDiagnostic(CharacterActionDiagnostic[] diagnostics, string code)
        {
            Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == code), "Missing diagnostic " + code);
        }
    }
}
