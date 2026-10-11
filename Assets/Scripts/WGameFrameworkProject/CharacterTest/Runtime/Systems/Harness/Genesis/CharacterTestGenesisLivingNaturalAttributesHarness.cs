using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGenesisLivingNaturalAttributesHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultCohesionLimit = 100;

        private const string StateModel = "harness-only";
        private const float DefaultBodyScaleMeters = 1.6f;
        private const float DefaultMassKg = 70f;
        private const int DefaultContactSurfaceCount = 1;
        private const int DefaultNearFieldContactCount = 1;
        private const int RecoveryRate = 24;

        internal static CharacterTestGenesisLivingNaturalAttributesValidation RunProbe(
            CharacterTestGenesisLivingNaturalAttributesProbeKind scenario,
            RuntimeFrame frame)
        {
            return RunValidationSuite(scenario, frame);
        }

        internal static CharacterTestGenesisLivingNaturalAttributesValidation RunValidationSuite(
            CharacterTestGenesisLivingNaturalAttributesProbeKind scenario =
                CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite,
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisLivingNaturalAttributesSample embody = CreateSample(
                "living-natural-attributes.embody.whole-body",
                CharacterTestGenesisLivingNaturalAttributesProbeKind.Embody,
                CharacterTestGenesisLivingNaturalAttributeJudgement.Embody,
                frame,
                selfCohesion: DefaultCohesionLimit,
                externalD: 0,
                externalS: 0,
                scatterBefore: 0,
                scatterAfterInput: 0,
                scatterAfterRecovery: 0,
                recoveryTicks: 0,
                bodyBoundaryAfterInput: true,
                bodyBoundaryAfterRecovery: true,
                selfMaintainsAfterInput: true,
                canActAfter: true,
                recoveredToStable: false,
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                laterHook: "character-test-runtime-entity");
            CharacterTestGenesisLivingNaturalAttributesSample selfMaintain = CreateSample(
                "living-natural-attributes.self-maintain.pressure",
                CharacterTestGenesisLivingNaturalAttributesProbeKind.SelfMaintain,
                CharacterTestGenesisLivingNaturalAttributeJudgement.SelfMaintain,
                frame,
                selfCohesion: DefaultCohesionLimit,
                externalD: 42,
                externalS: 68,
                scatterBefore: 0,
                scatterAfterInput: 68,
                scatterAfterRecovery: 68,
                recoveryTicks: 0,
                bodyBoundaryAfterInput: true,
                bodyBoundaryAfterRecovery: true,
                selfMaintainsAfterInput: true,
                canActAfter: true,
                recoveredToStable: false,
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                laterHook: "gameplay-pressure-cohesion-limit");
            CharacterTestGenesisLivingNaturalAttributesSample recover = CreateSample(
                "living-natural-attributes.recover.input-release",
                CharacterTestGenesisLivingNaturalAttributesProbeKind.Recover,
                CharacterTestGenesisLivingNaturalAttributeJudgement.Recover,
                frame,
                selfCohesion: DefaultCohesionLimit,
                externalD: 56,
                externalS: 92,
                scatterBefore: 0,
                scatterAfterInput: 92,
                scatterAfterRecovery: ClampScatter(92 - (4 * RecoveryRate)),
                recoveryTicks: 4,
                bodyBoundaryAfterInput: true,
                bodyBoundaryAfterRecovery: true,
                selfMaintainsAfterInput: false,
                canActAfter: true,
                recoveredToStable: true,
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                laterHook: "runtime-frame-recovery");
            CharacterTestGenesisLivingNaturalAttributesSample inactiveMaterial = CreateSample(
                "living-natural-attributes.inactive.return-material",
                CharacterTestGenesisLivingNaturalAttributesProbeKind.InactiveMaterial,
                CharacterTestGenesisLivingNaturalAttributeJudgement.ReturnToMaterial,
                frame,
                selfCohesion: 0,
                externalD: 130,
                externalS: 158,
                scatterBefore: 0,
                scatterAfterInput: 158,
                scatterAfterRecovery: 158,
                recoveryTicks: 5,
                bodyBoundaryAfterInput: false,
                bodyBoundaryAfterRecovery: false,
                selfMaintainsAfterInput: false,
                canActAfter: false,
                recoveredToStable: false,
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem,
                laterHook: "return-to-material-reading");

            return new CharacterTestGenesisLivingNaturalAttributesValidation(
                scenario,
                frame,
                StateModel,
                embody,
                selfMaintain,
                recover,
                inactiveMaterial);
        }

        private static CharacterTestGenesisLivingNaturalAttributesSample CreateSample(
            string sampleId,
            CharacterTestGenesisLivingNaturalAttributesProbeKind kind,
            CharacterTestGenesisLivingNaturalAttributeJudgement judgement,
            RuntimeFrame frame,
            int selfCohesion,
            int externalD,
            int externalS,
            int scatterBefore,
            int scatterAfterInput,
            int scatterAfterRecovery,
            int recoveryTicks,
            bool bodyBoundaryAfterInput,
            bool bodyBoundaryAfterRecovery,
            bool selfMaintainsAfterInput,
            bool canActAfter,
            bool recoveredToStable,
            CharacterTestGenesisLivingNaturalAttributeMaterialReading materialReadingAfter,
            string laterHook)
        {
            int recoveryAmount = ClampScatter(scatterAfterInput - scatterAfterRecovery);
            return new CharacterTestGenesisLivingNaturalAttributesSample(
                sampleId,
                kind,
                judgement,
                frame,
                StateModel,
                bodyBoundaryPresent: true,
                DefaultBodyScaleMeters,
                DefaultMassKg,
                DefaultContactSurfaceCount,
                DefaultNearFieldContactCount,
                selfCohesion,
                DefaultCohesionLimit,
                externalD,
                externalS,
                scatterBefore,
                scatterAfterInput,
                scatterAfterRecovery,
                recoveryTicks,
                recoveryAmount,
                bodyBoundaryAfterInput,
                bodyBoundaryAfterRecovery,
                selfMaintainsAfterInput,
                canActAfter,
                recoveredToStable,
                materialReadingAfter,
                laterHook);
        }

        private static int ClampScatter(int scatter)
        {
            return scatter < 0 ? 0 : scatter;
        }
    }
}
