using System;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisLivingNaturalAttributeJudgement
    {
        None = 0,
        Embody = 1,
        SelfMaintain = 2,
        Recover = 3,
        ReturnToMaterial = 4
    }

    internal enum CharacterTestGenesisLivingNaturalAttributeMaterialReading
    {
        LivingBody = 1,
        MaterialSystem = 2
    }

    internal readonly struct CharacterTestGenesisLivingNaturalAttributesValidation
    {
        public static readonly CharacterTestGenesisLivingNaturalAttributesValidation Empty =
            new CharacterTestGenesisLivingNaturalAttributesValidation(
                CharacterTestGenesisLivingNaturalAttributesProbeKind.None,
                default,
                "harness-only",
                CharacterTestGenesisLivingNaturalAttributesSample.Empty,
                CharacterTestGenesisLivingNaturalAttributesSample.Empty,
                CharacterTestGenesisLivingNaturalAttributesSample.Empty,
                CharacterTestGenesisLivingNaturalAttributesSample.Empty);

        public CharacterTestGenesisLivingNaturalAttributesValidation(
            CharacterTestGenesisLivingNaturalAttributesProbeKind scenario,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisLivingNaturalAttributesSample embody,
            CharacterTestGenesisLivingNaturalAttributesSample selfMaintain,
            CharacterTestGenesisLivingNaturalAttributesSample recover,
            CharacterTestGenesisLivingNaturalAttributesSample inactiveMaterial)
        {
            Scenario = scenario;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            Embody = embody;
            SelfMaintain = selfMaintain;
            Recover = recover;
            InactiveMaterial = inactiveMaterial;
        }

        public CharacterTestGenesisLivingNaturalAttributesProbeKind Scenario { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisLivingNaturalAttributesSample Embody { get; }
        public CharacterTestGenesisLivingNaturalAttributesSample SelfMaintain { get; }
        public CharacterTestGenesisLivingNaturalAttributesSample Recover { get; }
        public CharacterTestGenesisLivingNaturalAttributesSample InactiveMaterial { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} scenario={1} frame={2} embody=[{3}] selfMaintain=[{4}] recover=[{5}] inactiveMaterial=[{6}]",
                StateModel,
                Scenario,
                Frame.Value,
                Embody.ToSummaryLine(),
                SelfMaintain.ToSummaryLine(),
                Recover.ToSummaryLine(),
                InactiveMaterial.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisLivingNaturalAttributesSample
    {
        public static readonly CharacterTestGenesisLivingNaturalAttributesSample Empty =
            new CharacterTestGenesisLivingNaturalAttributesSample(
                string.Empty,
                CharacterTestGenesisLivingNaturalAttributesProbeKind.None,
                CharacterTestGenesisLivingNaturalAttributeJudgement.None,
                RuntimeFrame.Zero,
                "harness-only",
                bodyBoundaryPresent: false,
                bodyScaleMeters: 0f,
                massKg: 0f,
                contactSurfaceCount: 0,
                nearFieldContactCount: 0,
                selfCohesion: 0,
                cohesionLimit: 1,
                externalD: 0,
                externalS: 0,
                scatterBefore: 0,
                scatterAfterInput: 0,
                scatterAfterRecovery: 0,
                recoveryTicks: 0,
                recoveryAmount: 0,
                bodyBoundaryAfterInput: false,
                bodyBoundaryAfterRecovery: false,
                selfMaintainsAfterInput: false,
                canActAfter: false,
                recoveredToStable: false,
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                "none");

        public CharacterTestGenesisLivingNaturalAttributesSample(
            string sampleId,
            CharacterTestGenesisLivingNaturalAttributesProbeKind kind,
            CharacterTestGenesisLivingNaturalAttributeJudgement judgement,
            RuntimeFrame frame,
            string stateModel,
            bool bodyBoundaryPresent,
            float bodyScaleMeters,
            float massKg,
            int contactSurfaceCount,
            int nearFieldContactCount,
            int selfCohesion,
            int cohesionLimit,
            int externalD,
            int externalS,
            int scatterBefore,
            int scatterAfterInput,
            int scatterAfterRecovery,
            int recoveryTicks,
            int recoveryAmount,
            bool bodyBoundaryAfterInput,
            bool bodyBoundaryAfterRecovery,
            bool selfMaintainsAfterInput,
            bool canActAfter,
            bool recoveredToStable,
            CharacterTestGenesisLivingNaturalAttributeMaterialReading materialReadingAfter,
            string laterHook)
        {
            SampleId = sampleId ?? string.Empty;
            Kind = kind;
            Judgement = judgement;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            BodyBoundaryPresent = bodyBoundaryPresent;
            BodyScaleMeters = bodyScaleMeters < 0f ? 0f : bodyScaleMeters;
            MassKg = massKg < 0f ? 0f : massKg;
            ContactSurfaceCount = contactSurfaceCount < 0 ? 0 : contactSurfaceCount;
            NearFieldContactCount = nearFieldContactCount < 0 ? 0 : nearFieldContactCount;
            SelfCohesion = selfCohesion < 0 ? 0 : selfCohesion;
            CohesionLimit = cohesionLimit <= 0 ? 1 : cohesionLimit;
            ExternalD = externalD < 0 ? 0 : externalD;
            ExternalS = externalS < 0 ? 0 : externalS;
            ScatterBefore = scatterBefore < 0 ? 0 : scatterBefore;
            ScatterAfterInput = scatterAfterInput < 0 ? 0 : scatterAfterInput;
            ScatterAfterRecovery = scatterAfterRecovery < 0 ? 0 : scatterAfterRecovery;
            RecoveryTicks = recoveryTicks < 0 ? 0 : recoveryTicks;
            RecoveryAmount = recoveryAmount < 0 ? 0 : recoveryAmount;
            BodyBoundaryAfterInput = bodyBoundaryAfterInput;
            BodyBoundaryAfterRecovery = bodyBoundaryAfterRecovery;
            SelfMaintainsAfterInput = selfMaintainsAfterInput;
            CanActAfter = canActAfter;
            RecoveredToStable = recoveredToStable;
            MaterialReadingAfter = materialReadingAfter;
            LaterHook = string.IsNullOrWhiteSpace(laterHook) ? "none" : laterHook;
        }

        public string SampleId { get; }
        public CharacterTestGenesisLivingNaturalAttributesProbeKind Kind { get; }
        public CharacterTestGenesisLivingNaturalAttributeJudgement Judgement { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public bool BodyBoundaryPresent { get; }
        public float BodyScaleMeters { get; }
        public float MassKg { get; }
        public int ContactSurfaceCount { get; }
        public int NearFieldContactCount { get; }
        public int SelfCohesion { get; }
        public int CohesionLimit { get; }
        public int ExternalD { get; }
        public int ExternalS { get; }
        public int ScatterBefore { get; }
        public int ScatterAfterInput { get; }
        public int ScatterAfterRecovery { get; }
        public int RecoveryTicks { get; }
        public int RecoveryAmount { get; }
        public bool BodyBoundaryAfterInput { get; }
        public bool BodyBoundaryAfterRecovery { get; }
        public bool SelfMaintainsAfterInput { get; }
        public bool CanActAfter { get; }
        public bool RecoveredToStable { get; }
        public CharacterTestGenesisLivingNaturalAttributeMaterialReading MaterialReadingAfter { get; }
        public string LaterHook { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);
        public int ScatterClearedByRecovery => Math.Max(0, ScatterAfterInput - ScatterAfterRecovery);
        public bool UsesWholeBody =>
            BodyBoundaryPresent
            && BodyScaleMeters > 0f
            && MassKg > 0f
            && ContactSurfaceCount > 0
            && NearFieldContactCount > 0;
        public bool CanEmbody => UsesWholeBody && BodyBoundaryPresent;
        public bool CanSelfMaintain =>
            SelfMaintainsAfterInput
            && BodyBoundaryAfterInput
            && SelfCohesion >= ScatterAfterInput
            && MaterialReadingAfter == CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool CanRecover =>
            RecoveredToStable
            && BodyBoundaryAfterRecovery
            && ScatterAfterRecovery < CohesionLimit
            && MaterialReadingAfter == CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool ReturnedToMaterial =>
            MaterialReadingAfter == CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem
            && !BodyBoundaryAfterRecovery
            && !CanActAfter;

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "sampleId={0} kind={1} judgement={2} stateModel={3} frame={4} wholeBody=boundary:{5},scale:{6:0.###},massKg:{7:0.###},contactSurfaces:{8},nearContacts:{9} cohesion=self:{10},limit:{11} inputDPS=externalD:{12},externalS:{13} scatter={14}->{15}->{16} recoveryTicks={17} recoveryAmount={18} clearedByRecovery={19} boundaryAfter=input:{20},recovery:{21} selfMaintainsAfterInput={22} canEmbody={23} canSelfMaintain={24} canRecover={25} canActAfter={26} recoveredToStable={27} materialReadingAfter={28} returnedToMaterial={29} laterHook={30}",
                SampleId,
                Kind,
                Judgement,
                StateModel,
                Frame.Value,
                BodyBoundaryPresent ? "true" : "false",
                BodyScaleMeters,
                MassKg,
                ContactSurfaceCount,
                NearFieldContactCount,
                SelfCohesion,
                CohesionLimit,
                ExternalD,
                ExternalS,
                ScatterBefore,
                ScatterAfterInput,
                ScatterAfterRecovery,
                RecoveryTicks,
                RecoveryAmount,
                ScatterClearedByRecovery,
                BodyBoundaryAfterInput ? "true" : "false",
                BodyBoundaryAfterRecovery ? "true" : "false",
                SelfMaintainsAfterInput ? "true" : "false",
                CanEmbody ? "true" : "false",
                CanSelfMaintain ? "true" : "false",
                CanRecover ? "true" : "false",
                CanActAfter ? "true" : "false",
                RecoveredToStable ? "true" : "false",
                MaterialReadingAfter,
                ReturnedToMaterial ? "true" : "false",
                LaterHook);
        }
    }
}
