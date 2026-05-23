using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterReactionContextBuilderTests
    {
        [Test]
        public void PostureBreak_BuildsPressureOnlyContextWithIncompleteHitDiagnostics()
        {
            CharacterReactionContextBuildResult result = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    new RuntimeFrame(10),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 20,
                    sourceId: 7,
                    reason: "posture-overflow",
                    traceId: "pb-1"));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, result.Completeness);
            Assert.AreEqual(CharacterReactionContextSourceKind.PostureBreak, result.Context.SourceKind);
            Assert.AreEqual(Entity(), result.Context.EntityId);
            Assert.AreEqual(PressureBand.Critical, result.Context.PreviousPressureBand);
            Assert.AreEqual(PressureBand.Broken, result.Context.CurrentPressureBand);
            Assert.AreEqual(100, result.Context.CurrentPressure);
            Assert.AreEqual(7, result.Context.SourceId);
            Assert.AreEqual("posture-overflow", result.Context.Reason);
            Assert.AreEqual("pb-1", result.Context.TraceId);
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
                    traceId: "guard"));
            CharacterReactionContextBuildResult armor = CharacterReactionContextBuilder.FromArmorBreak(
                new ArmorBreakEvent(
                    new RuntimeFrame(2),
                    Entity(),
                    previousIntegrity: 12,
                    currentIntegrity: 0,
                    maxIntegrity: 12,
                    incomingDamage: 18,
                    traceId: "armor"));
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
                    reason: "pressure",
                    traceId: "band"));

            Assert.AreEqual(CharacterReactionContextSourceKind.GuardBreak, guard.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, guard.Completeness);
            Assert.AreEqual(PressureBand.Broken, guard.Context.CurrentPressureBand);
            AssertHitFieldsAreEmpty(guard.Context);

            Assert.AreEqual(CharacterReactionContextSourceKind.ArmorBreak, armor.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, armor.Completeness);
            Assert.AreEqual(12, armor.Context.PreviousPressure);
            Assert.AreEqual(0, armor.Context.CurrentPressure);
            Assert.AreEqual(-12, armor.Context.Delta);
            AssertHitFieldsAreEmpty(armor.Context);

            Assert.AreEqual(CharacterReactionContextSourceKind.PressureBandChanged, band.Context.SourceKind);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, band.Completeness);
            Assert.AreEqual(PressureBand.Pressed, band.Context.PreviousPressureBand);
            Assert.AreEqual(PressureBand.Critical, band.Context.CurrentPressureBand);
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
                traceId: "death");
            CharacterReactionContextBuildResult lifecycle = CharacterReactionContextBuilder.FromLifecycle(
                new RuntimeFrame(21),
                Entity(),
                lifecycleState: "PendingDestroy",
                reason: "cleanup",
                traceId: "life");

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
        public void ReactionSelector_SelectsPostureBreakReactAndDeathUnderPressureOnlyScope()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule("Death", CharacterReactionRuleTrigger.Death),
                    new CharacterReactionRule("PostureBreakReact", CharacterReactionRuleTrigger.PostureBreak)
                });
            CharacterReactionContext posture = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 10)).Context;
            CharacterReactionContext death = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(9),
                Entity()).Context;

            CharacterReactionSelectionResult postureResult = CharacterReactionSelector.Select(profile, posture);
            CharacterReactionSelectionResult deathResult = CharacterReactionSelector.Select(profile, death);

            Assert.IsTrue(postureResult.Accepted);
            Assert.AreEqual("PostureBreakReact", postureResult.SelectedActionId);
            AssertDiagnostic(postureResult.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            Assert.IsTrue(deathResult.Accepted);
            Assert.AreEqual("Death", deathResult.SelectedActionId);
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
                    new CharacterReactionRule("AnyCriticalReact", CharacterReactionRuleTrigger.Any, currentPressureBand: PressureBand.Broken, priority: 100),
                    new CharacterReactionRule("PostureBreakReact", CharacterReactionRuleTrigger.PostureBreak, priority: 1),
                    new CharacterReactionRule("PostureBreakHighPriority", CharacterReactionRuleTrigger.PostureBreak, priority: 10),
                    new CharacterReactionRule("PostureBreakLaterTie", CharacterReactionRuleTrigger.PostureBreak, priority: 10),
                });
            CharacterReactionContext posture = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 10)).Context;

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, posture);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual("PostureBreakHighPriority", result.SelectedActionId);
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
                    new CharacterReactionRule("PostureBreakReact", CharacterReactionRuleTrigger.PostureBreak),
                },
                defaultActionId: "LightHitReact");
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
            Assert.AreEqual("LightHitReact", result.SelectedActionId);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionFallbackUsed);
        }

        [Test]
        public void HitContextRule_ReturnsStableErrorUnderPressureOnlyContext()
        {
            var profile = new CharacterReactionProfile(
                "directional",
                new[]
                {
                    new CharacterReactionRule(
                        "FrontBodyHitReact",
                        CharacterReactionRuleTrigger.PostureBreak,
                        requiresBodyPart: true,
                        requiresHitDirection: true)
                });
            CharacterReactionContext context = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 10)).Context;

            CharacterReactionSelectionResult result = CharacterReactionSelector.Select(profile, context);
            CharacterActionDiagnostic[] validatorDiagnostics = CharacterReactionRuleValidator.ValidateAgainstContext(
                profile.Rules[0],
                context);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext, result.RejectCode);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext);
            AssertDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionContextIncomplete);
            AssertDiagnostic(validatorDiagnostics, CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext);
        }

        private static GameplayEntityId Entity()
        {
            return new GameplayEntityId(1, 1);
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
