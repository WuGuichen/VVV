using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGenesisLivingBeingHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultCohesionLimit = 100;

        private const string StateModel = "harness-only";
        private const float DefaultBodyScaleMeters = 1.6f;
        private const float DefaultMassKg = 70f;
        private const int DefaultContactSurfaceCount = 1;
        private const int DefaultNearFieldContactCount = 1;
        private const int UnbalancedThreshold = 70;
        private const int BreakThreshold = 100;
        private const int InactiveThreshold = 145;
        private const int RecoveryRate = 12;

        internal static CharacterTestGenesisLivingBeingValidation RunProbe(
            CharacterTestGenesisLivingBeingProbeKind scenario,
            RuntimeFrame frame)
        {
            return RunValidationSuite(scenario, frame);
        }

        internal static CharacterTestGenesisLivingBeingValidation RunValidationSuite(
            CharacterTestGenesisLivingBeingProbeKind scenario =
                CharacterTestGenesisLivingBeingProbeKind.ValidationSuite,
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisLivingBeingSample approach = RunActionSample(
                "living-being.approach.near-contact",
                CharacterTestGenesisLivingBeingProbeKind.Approach,
                CharacterTestGenesisLivingBeingActionVerb.Approach,
                frame,
                distanceBeforeMeters: 2.0f,
                distanceAfterMeters: 1.25f,
                actionD: 24);
            CharacterTestGenesisLivingBeingSample avoid = RunActionSample(
                "living-being.avoid.pressure",
                CharacterTestGenesisLivingBeingProbeKind.Avoid,
                CharacterTestGenesisLivingBeingActionVerb.Avoid,
                frame,
                distanceBeforeMeters: 0.45f,
                distanceAfterMeters: 1.15f,
                actionD: 22,
                externalS: 18);
            CharacterTestGenesisLivingBeingSample brace = RunBraceSample(frame);
            CharacterTestGenesisLivingBeingSample unbalanced = RunPressureSample(
                "living-being.state.unbalanced",
                CharacterTestGenesisLivingBeingProbeKind.Unbalanced,
                frame,
                externalD: 40,
                externalS: 78,
                recoveryTicks: 0,
                inactive: false);
            CharacterTestGenesisLivingBeingSample cohesionBroken = RunPressureSample(
                "living-being.state.cohesion-broken",
                CharacterTestGenesisLivingBeingProbeKind.CohesionBroken,
                frame,
                externalD: 86,
                externalS: 122,
                recoveryTicks: 0,
                inactive: false);
            CharacterTestGenesisLivingBeingSample restabilize = RunPressureSample(
                "living-being.restabilize.self-cohesion",
                CharacterTestGenesisLivingBeingProbeKind.Restabilize,
                frame,
                externalD: 36,
                externalS: 76,
                recoveryTicks: 5,
                inactive: false);
            CharacterTestGenesisLivingBeingSample inactive = RunPressureSample(
                "living-being.state.inactive-material",
                CharacterTestGenesisLivingBeingProbeKind.Inactive,
                frame,
                externalD: 120,
                externalS: 156,
                recoveryTicks: 6,
                inactive: true);

            return new CharacterTestGenesisLivingBeingValidation(
                scenario,
                frame,
                StateModel,
                approach,
                avoid,
                brace,
                unbalanced,
                cohesionBroken,
                restabilize,
                inactive);
        }

        private static CharacterTestGenesisLivingBeingSample RunActionSample(
            string sampleId,
            CharacterTestGenesisLivingBeingProbeKind kind,
            CharacterTestGenesisLivingBeingActionVerb verb,
            RuntimeFrame frame,
            float distanceBeforeMeters,
            float distanceAfterMeters,
            int actionD,
            int externalS = 0)
        {
            int scatterAfterInput = externalS;
            return CreateSample(
                sampleId,
                kind,
                verb,
                CharacterTestGenesisLivingBeingState.Stable,
                CharacterTestGenesisLivingBeingState.Stable,
                frame,
                actionD,
                externalD: 0,
                externalS,
                scatterBefore: 0,
                scatterAfterInput,
                scatterAfterRecovery: scatterAfterInput,
                cohesionAfter: DefaultCohesionLimit,
                distanceBeforeMeters,
                distanceAfterMeters,
                bodyDisplacementMeters: Abs(distanceBeforeMeters - distanceAfterMeters),
                resistedDisplacementMeters: 0f,
                recoveryTicks: 0,
                recoveryAmount: 0,
                selfMaintainsAfter: true,
                canActAfter: true,
                recoveredToStable: false,
                inactiveRecoveryAttempted: false,
                inactiveRecoverySucceeded: false,
                CharacterTestGenesisLivingBeingMaterialReading.LivingBody,
                laterHook: "contact-near-field");
        }

        private static CharacterTestGenesisLivingBeingSample RunBraceSample(RuntimeFrame frame)
        {
            const int externalD = 62;
            const int externalS = 88;
            const int braceSupport = 46;
            int scatterAfterInput = ClampScatter(externalS - braceSupport);

            return CreateSample(
                "living-being.brace.resist-pressure",
                CharacterTestGenesisLivingBeingProbeKind.Brace,
                CharacterTestGenesisLivingBeingActionVerb.Brace,
                CharacterTestGenesisLivingBeingState.Stable,
                CharacterTestGenesisLivingBeingState.Stable,
                frame,
                actionD: 30,
                externalD,
                externalS,
                scatterBefore: 0,
                scatterAfterInput,
                scatterAfterRecovery: scatterAfterInput,
                cohesionAfter: DefaultCohesionLimit + braceSupport,
                distanceBeforeMeters: 0.35f,
                distanceAfterMeters: 0.38f,
                bodyDisplacementMeters: 0.03f,
                resistedDisplacementMeters: 0.62f,
                recoveryTicks: 0,
                recoveryAmount: 0,
                selfMaintainsAfter: true,
                canActAfter: true,
                recoveredToStable: false,
                inactiveRecoveryAttempted: false,
                inactiveRecoverySucceeded: false,
                CharacterTestGenesisLivingBeingMaterialReading.LivingBody,
                laterHook: "brace-self-cohesion");
        }

        private static CharacterTestGenesisLivingBeingSample RunPressureSample(
            string sampleId,
            CharacterTestGenesisLivingBeingProbeKind kind,
            RuntimeFrame frame,
            int externalD,
            int externalS,
            int recoveryTicks,
            bool inactive)
        {
            int scatterAfterInput = externalS;
            CharacterTestGenesisLivingBeingState afterInput = ResolveState(scatterAfterInput, inactive);
            int recoveryAmount = inactive ? 0 : recoveryTicks * RecoveryRate;
            int scatterAfterRecovery = inactive
                ? scatterAfterInput
                : ClampScatter(scatterAfterInput - recoveryAmount);
            CharacterTestGenesisLivingBeingState finalState = ResolveState(scatterAfterRecovery, inactive);
            bool recoveredToStable = afterInput != CharacterTestGenesisLivingBeingState.Stable
                && finalState == CharacterTestGenesisLivingBeingState.Stable;

            return CreateSample(
                sampleId,
                kind,
                kind == CharacterTestGenesisLivingBeingProbeKind.Restabilize
                    ? CharacterTestGenesisLivingBeingActionVerb.Restabilize
                    : CharacterTestGenesisLivingBeingActionVerb.None,
                CharacterTestGenesisLivingBeingState.Stable,
                finalState,
                frame,
                actionD: kind == CharacterTestGenesisLivingBeingProbeKind.Restabilize ? 12 : 0,
                externalD,
                externalS,
                scatterBefore: 0,
                scatterAfterInput,
                scatterAfterRecovery,
                cohesionAfter: inactive ? 0 : DefaultCohesionLimit,
                distanceBeforeMeters: 0.6f,
                distanceAfterMeters: inactive ? 0.6f : 0.72f,
                bodyDisplacementMeters: inactive ? 0f : 0.12f,
                resistedDisplacementMeters: 0f,
                recoveryTicks,
                recoveryAmount,
                selfMaintainsAfter: !inactive,
                canActAfter: finalState == CharacterTestGenesisLivingBeingState.Stable,
                recoveredToStable,
                inactiveRecoveryAttempted: inactive && recoveryTicks > 0,
                inactiveRecoverySucceeded: false,
                inactive
                    ? CharacterTestGenesisLivingBeingMaterialReading.MaterialSystem
                    : CharacterTestGenesisLivingBeingMaterialReading.LivingBody,
                laterHook: inactive ? "return-to-material-system" : "self-recohesion");
        }

        private static CharacterTestGenesisLivingBeingSample CreateSample(
            string sampleId,
            CharacterTestGenesisLivingBeingProbeKind kind,
            CharacterTestGenesisLivingBeingActionVerb verb,
            CharacterTestGenesisLivingBeingState stateBefore,
            CharacterTestGenesisLivingBeingState stateAfter,
            RuntimeFrame frame,
            int actionD,
            int externalD,
            int externalS,
            int scatterBefore,
            int scatterAfterInput,
            int scatterAfterRecovery,
            int cohesionAfter,
            float distanceBeforeMeters,
            float distanceAfterMeters,
            float bodyDisplacementMeters,
            float resistedDisplacementMeters,
            int recoveryTicks,
            int recoveryAmount,
            bool selfMaintainsAfter,
            bool canActAfter,
            bool recoveredToStable,
            bool inactiveRecoveryAttempted,
            bool inactiveRecoverySucceeded,
            CharacterTestGenesisLivingBeingMaterialReading materialReadingAfter,
            string laterHook)
        {
            return new CharacterTestGenesisLivingBeingSample(
                sampleId,
                kind,
                verb,
                stateBefore,
                stateAfter,
                frame,
                StateModel,
                DefaultBodyScaleMeters,
                DefaultMassKg,
                DefaultCohesionLimit,
                DefaultContactSurfaceCount,
                DefaultNearFieldContactCount,
                selfMaintainsBoundaryBefore: true,
                selfMaintainsAfter,
                actionD,
                externalD,
                externalS,
                scatterBefore,
                scatterAfterInput,
                scatterAfterRecovery,
                cohesionBefore: DefaultCohesionLimit,
                cohesionAfter,
                distanceBeforeMeters,
                distanceAfterMeters,
                bodyDisplacementMeters,
                resistedDisplacementMeters,
                recoveryTicks,
                recoveryAmount,
                unbalancedThresholdMet: scatterAfterInput >= UnbalancedThreshold,
                breakThresholdMet: scatterAfterInput >= BreakThreshold,
                inactiveThresholdMet: scatterAfterInput >= InactiveThreshold,
                canActAfter,
                recoveredToStable,
                inactiveRecoveryAttempted,
                inactiveRecoverySucceeded,
                materialReadingAfter,
                laterHook);
        }

        private static CharacterTestGenesisLivingBeingState ResolveState(int scatter, bool inactive)
        {
            if (inactive || scatter >= InactiveThreshold)
                return CharacterTestGenesisLivingBeingState.Inactive;

            if (scatter >= BreakThreshold)
                return CharacterTestGenesisLivingBeingState.CohesionBroken;

            if (scatter >= UnbalancedThreshold)
                return CharacterTestGenesisLivingBeingState.Unbalanced;

            return CharacterTestGenesisLivingBeingState.Stable;
        }

        private static int ClampScatter(int scatter)
        {
            return scatter < 0 ? 0 : scatter;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}
