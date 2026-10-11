using System;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisLivingBeingActionVerb
    {
        None = 0,
        Approach = 1,
        Avoid = 2,
        Brace = 3,
        Restabilize = 4
    }

    internal enum CharacterTestGenesisLivingBeingState
    {
        Stable = 1,
        Unbalanced = 2,
        CohesionBroken = 3,
        Inactive = 4
    }

    internal enum CharacterTestGenesisLivingBeingMaterialReading
    {
        LivingBody = 1,
        MaterialSystem = 2
    }

    internal readonly struct CharacterTestGenesisLivingBeingValidation
    {
        public static readonly CharacterTestGenesisLivingBeingValidation Empty =
            new CharacterTestGenesisLivingBeingValidation(
                CharacterTestGenesisLivingBeingProbeKind.None,
                default,
                "harness-only",
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty,
                CharacterTestGenesisLivingBeingSample.Empty);

        public CharacterTestGenesisLivingBeingValidation(
            CharacterTestGenesisLivingBeingProbeKind scenario,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisLivingBeingSample approach,
            CharacterTestGenesisLivingBeingSample avoid,
            CharacterTestGenesisLivingBeingSample brace,
            CharacterTestGenesisLivingBeingSample unbalanced,
            CharacterTestGenesisLivingBeingSample cohesionBroken,
            CharacterTestGenesisLivingBeingSample restabilize,
            CharacterTestGenesisLivingBeingSample inactive)
        {
            Scenario = scenario;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            Approach = approach;
            Avoid = avoid;
            Brace = brace;
            Unbalanced = unbalanced;
            CohesionBroken = cohesionBroken;
            Restabilize = restabilize;
            Inactive = inactive;
        }

        public CharacterTestGenesisLivingBeingProbeKind Scenario { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisLivingBeingSample Approach { get; }
        public CharacterTestGenesisLivingBeingSample Avoid { get; }
        public CharacterTestGenesisLivingBeingSample Brace { get; }
        public CharacterTestGenesisLivingBeingSample Unbalanced { get; }
        public CharacterTestGenesisLivingBeingSample CohesionBroken { get; }
        public CharacterTestGenesisLivingBeingSample Restabilize { get; }
        public CharacterTestGenesisLivingBeingSample Inactive { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} scenario={1} frame={2} approach=[{3}] avoid=[{4}] brace=[{5}] unbalanced=[{6}] cohesionBroken=[{7}] restabilize=[{8}] inactive=[{9}]",
                StateModel,
                Scenario,
                Frame.Value,
                Approach.ToSummaryLine(),
                Avoid.ToSummaryLine(),
                Brace.ToSummaryLine(),
                Unbalanced.ToSummaryLine(),
                CohesionBroken.ToSummaryLine(),
                Restabilize.ToSummaryLine(),
                Inactive.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisLivingBeingSample
    {
        public static readonly CharacterTestGenesisLivingBeingSample Empty =
            new CharacterTestGenesisLivingBeingSample(
                string.Empty,
                CharacterTestGenesisLivingBeingProbeKind.None,
                CharacterTestGenesisLivingBeingActionVerb.None,
                CharacterTestGenesisLivingBeingState.Stable,
                CharacterTestGenesisLivingBeingState.Stable,
                RuntimeFrame.Zero,
                "harness-only",
                bodyScaleMeters: 0f,
                massKg: 0f,
                cohesionLimit: 1,
                contactSurfaceCount: 0,
                nearFieldContactCount: 0,
                selfMaintainsBoundaryBefore: false,
                selfMaintainsBoundaryAfter: false,
                actionD: 0,
                externalD: 0,
                externalS: 0,
                scatterBefore: 0,
                scatterAfterInput: 0,
                scatterAfterRecovery: 0,
                cohesionBefore: 0,
                cohesionAfter: 0,
                distanceBeforeMeters: 0f,
                distanceAfterMeters: 0f,
                bodyDisplacementMeters: 0f,
                resistedDisplacementMeters: 0f,
                recoveryTicks: 0,
                recoveryAmount: 0,
                unbalancedThresholdMet: false,
                breakThresholdMet: false,
                inactiveThresholdMet: false,
                canActAfter: false,
                recoveredToStable: false,
                inactiveRecoveryAttempted: false,
                inactiveRecoverySucceeded: false,
                CharacterTestGenesisLivingBeingMaterialReading.LivingBody,
                "none");

        public CharacterTestGenesisLivingBeingSample(
            string sampleId,
            CharacterTestGenesisLivingBeingProbeKind kind,
            CharacterTestGenesisLivingBeingActionVerb actionVerb,
            CharacterTestGenesisLivingBeingState stateBefore,
            CharacterTestGenesisLivingBeingState stateAfter,
            RuntimeFrame frame,
            string stateModel,
            float bodyScaleMeters,
            float massKg,
            int cohesionLimit,
            int contactSurfaceCount,
            int nearFieldContactCount,
            bool selfMaintainsBoundaryBefore,
            bool selfMaintainsBoundaryAfter,
            int actionD,
            int externalD,
            int externalS,
            int scatterBefore,
            int scatterAfterInput,
            int scatterAfterRecovery,
            int cohesionBefore,
            int cohesionAfter,
            float distanceBeforeMeters,
            float distanceAfterMeters,
            float bodyDisplacementMeters,
            float resistedDisplacementMeters,
            int recoveryTicks,
            int recoveryAmount,
            bool unbalancedThresholdMet,
            bool breakThresholdMet,
            bool inactiveThresholdMet,
            bool canActAfter,
            bool recoveredToStable,
            bool inactiveRecoveryAttempted,
            bool inactiveRecoverySucceeded,
            CharacterTestGenesisLivingBeingMaterialReading materialReadingAfter,
            string laterHook)
        {
            SampleId = sampleId ?? string.Empty;
            Kind = kind;
            ActionVerb = actionVerb;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            BodyScaleMeters = bodyScaleMeters < 0f ? 0f : bodyScaleMeters;
            MassKg = massKg < 0f ? 0f : massKg;
            CohesionLimit = cohesionLimit <= 0 ? 1 : cohesionLimit;
            ContactSurfaceCount = contactSurfaceCount < 0 ? 0 : contactSurfaceCount;
            NearFieldContactCount = nearFieldContactCount < 0 ? 0 : nearFieldContactCount;
            SelfMaintainsBoundaryBefore = selfMaintainsBoundaryBefore;
            SelfMaintainsBoundaryAfter = selfMaintainsBoundaryAfter;
            ActionD = actionD < 0 ? 0 : actionD;
            ExternalD = externalD < 0 ? 0 : externalD;
            ExternalS = externalS < 0 ? 0 : externalS;
            ScatterBefore = scatterBefore < 0 ? 0 : scatterBefore;
            ScatterAfterInput = scatterAfterInput < 0 ? 0 : scatterAfterInput;
            ScatterAfterRecovery = scatterAfterRecovery < 0 ? 0 : scatterAfterRecovery;
            CohesionBefore = cohesionBefore < 0 ? 0 : cohesionBefore;
            CohesionAfter = cohesionAfter < 0 ? 0 : cohesionAfter;
            DistanceBeforeMeters = distanceBeforeMeters < 0f ? 0f : distanceBeforeMeters;
            DistanceAfterMeters = distanceAfterMeters < 0f ? 0f : distanceAfterMeters;
            BodyDisplacementMeters = bodyDisplacementMeters < 0f ? 0f : bodyDisplacementMeters;
            ResistedDisplacementMeters = resistedDisplacementMeters < 0f ? 0f : resistedDisplacementMeters;
            RecoveryTicks = recoveryTicks < 0 ? 0 : recoveryTicks;
            RecoveryAmount = recoveryAmount < 0 ? 0 : recoveryAmount;
            UnbalancedThresholdMet = unbalancedThresholdMet;
            BreakThresholdMet = breakThresholdMet;
            InactiveThresholdMet = inactiveThresholdMet;
            CanActAfter = canActAfter;
            RecoveredToStable = recoveredToStable;
            InactiveRecoveryAttempted = inactiveRecoveryAttempted;
            InactiveRecoverySucceeded = inactiveRecoverySucceeded;
            MaterialReadingAfter = materialReadingAfter;
            LaterHook = string.IsNullOrWhiteSpace(laterHook) ? "none" : laterHook;
        }

        public string SampleId { get; }
        public CharacterTestGenesisLivingBeingProbeKind Kind { get; }
        public CharacterTestGenesisLivingBeingActionVerb ActionVerb { get; }
        public CharacterTestGenesisLivingBeingState StateBefore { get; }
        public CharacterTestGenesisLivingBeingState StateAfter { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public float BodyScaleMeters { get; }
        public float MassKg { get; }
        public int CohesionLimit { get; }
        public int ContactSurfaceCount { get; }
        public int NearFieldContactCount { get; }
        public bool SelfMaintainsBoundaryBefore { get; }
        public bool SelfMaintainsBoundaryAfter { get; }
        public int ActionD { get; }
        public int ExternalD { get; }
        public int ExternalS { get; }
        public int ScatterBefore { get; }
        public int ScatterAfterInput { get; }
        public int ScatterAfterRecovery { get; }
        public int CohesionBefore { get; }
        public int CohesionAfter { get; }
        public float DistanceBeforeMeters { get; }
        public float DistanceAfterMeters { get; }
        public float BodyDisplacementMeters { get; }
        public float ResistedDisplacementMeters { get; }
        public int RecoveryTicks { get; }
        public int RecoveryAmount { get; }
        public bool UnbalancedThresholdMet { get; }
        public bool BreakThresholdMet { get; }
        public bool InactiveThresholdMet { get; }
        public bool CanActAfter { get; }
        public bool RecoveredToStable { get; }
        public bool InactiveRecoveryAttempted { get; }
        public bool InactiveRecoverySucceeded { get; }
        public CharacterTestGenesisLivingBeingMaterialReading MaterialReadingAfter { get; }
        public string LaterHook { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);
        public int ScatterClearedByRecovery => Math.Max(0, ScatterAfterInput - ScatterAfterRecovery);
        public bool UsesWholeBody =>
            BodyScaleMeters > 0f
            && MassKg > 0f
            && CohesionLimit > 0
            && ContactSurfaceCount > 0;

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "sampleId={0} kind={1} verb={2} stateModel={3} state={4}->{5} frame={6} wholeBody=scale:{7:0.###},massKg:{8:0.###},cohesionLimit:{9},contactSurfaces:{10},nearContacts:{11} selfMaintain={12}->{13} inputDPS=actionD:{14},externalD:{15},externalS:{16} scatter={17}->{18}->{19} cohesion={20}->{21} thresholds=unbalanced:{22},break:{23},inactive:{24} distance={25:0.###}->{26:0.###} displacement={27:0.###} resistedDisplacement={28:0.###} recoveryTicks={29} recoveryAmount={30} clearedByRecovery={31} canActAfter={32} recoveredToStable={33} inactiveRecoveryAttempted={34} inactiveRecoverySucceeded={35} materialReadingAfter={36} laterHook={37}",
                SampleId,
                Kind,
                ActionVerb,
                StateModel,
                StateBefore,
                StateAfter,
                Frame.Value,
                BodyScaleMeters,
                MassKg,
                CohesionLimit,
                ContactSurfaceCount,
                NearFieldContactCount,
                SelfMaintainsBoundaryBefore ? "true" : "false",
                SelfMaintainsBoundaryAfter ? "true" : "false",
                ActionD,
                ExternalD,
                ExternalS,
                ScatterBefore,
                ScatterAfterInput,
                ScatterAfterRecovery,
                CohesionBefore,
                CohesionAfter,
                UnbalancedThresholdMet ? "true" : "false",
                BreakThresholdMet ? "true" : "false",
                InactiveThresholdMet ? "true" : "false",
                DistanceBeforeMeters,
                DistanceAfterMeters,
                BodyDisplacementMeters,
                ResistedDisplacementMeters,
                RecoveryTicks,
                RecoveryAmount,
                ScatterClearedByRecovery,
                CanActAfter ? "true" : "false",
                RecoveredToStable ? "true" : "false",
                InactiveRecoveryAttempted ? "true" : "false",
                InactiveRecoverySucceeded ? "true" : "false",
                MaterialReadingAfter,
                LaterHook);
        }
    }
}
