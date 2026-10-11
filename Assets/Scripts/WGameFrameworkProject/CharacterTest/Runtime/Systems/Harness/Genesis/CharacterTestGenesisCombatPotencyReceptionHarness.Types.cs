using System;
using System.Globalization;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisCombatPotencyReceptionKind
    {
        HeavyHammer = 1,
        LightBlade = 2,
        ShieldCharge = 3,
        Flame = 4,
    }

    internal readonly struct CharacterTestGenesisCombatPotencyReceptionValidation
    {
        public CharacterTestGenesisCombatPotencyReceptionValidation(
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisCombatPotencyReceptionSample heavyHammer,
            CharacterTestGenesisCombatPotencyReceptionSample lightBlade,
            CharacterTestGenesisCombatPotencyReceptionSample shieldCharge,
            CharacterTestGenesisCombatPotencyReceptionSample flame)
        {
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            HeavyHammer = heavyHammer;
            LightBlade = lightBlade;
            ShieldCharge = shieldCharge;
            Flame = flame;
        }

        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisCombatPotencyReceptionSample HeavyHammer { get; }
        public CharacterTestGenesisCombatPotencyReceptionSample LightBlade { get; }
        public CharacterTestGenesisCombatPotencyReceptionSample ShieldCharge { get; }
        public CharacterTestGenesisCombatPotencyReceptionSample Flame { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} frame={1} heavyHammer=[{2}] lightBlade=[{3}] shieldCharge=[{4}] flame=[{5}]",
                StateModel,
                Frame.Value,
                HeavyHammer.ToSummaryLine(),
                LightBlade.ToSummaryLine(),
                ShieldCharge.ToSummaryLine(),
                Flame.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisCombatPotencyReceptionSample
    {
        public CharacterTestGenesisCombatPotencyReceptionSample(
            CharacterTestGenesisCombatPotencyReceptionKind kind,
            string sampleId,
            string stateModel,
            int inputD,
            int inputS,
            bool isContinuousInput,
            int impactPower,
            float sourceMassKg,
            float targetMassKg,
            float hitZoneImpulseScale,
            float massTransfer,
            float effectiveImpact,
            int scatterImpact,
            int hitCount,
            int hitCountToBreak,
            int scatterPressureBefore,
            int scatterPressureAfterFirstHit,
            int scatterPressureAfterInputs,
            int scatterPressureBeforeRecovery,
            int scatterPressureAfterRecovery,
            int cohesionLimitBefore,
            int cohesionLimit,
            int maxScatterPressure,
            bool cohesionSupportActive,
            bool cohesionSupportWindowActive,
            float cohesionSupportRate,
            int supportLocalFrame,
            PressureBand bandAfterFirstHit,
            PressureBand bandAfterInputs,
            PressureBand bandAfterRecovery,
            int pressureChangedEventCount,
            int scatterImpactResolvedEventCount,
            int pressureBandChangedEventCount,
            int cohesionBreakEventCount,
            int recoveryEventCount,
            long firstCohesionBreakFrame,
            int recoveryDelayFrames,
            int recoveryRate,
            int recoveryTickCount,
            float previewKnockbackDistanceMeters,
            float knockbackSpeed,
            string laterHook)
        {
            Kind = kind;
            SampleId = sampleId ?? string.Empty;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            InputD = inputD < 0 ? 0 : inputD;
            InputS = inputS < 0 ? 0 : inputS;
            IsContinuousInput = isContinuousInput;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            SourceMassKg = sourceMassKg < 0f ? 0f : sourceMassKg;
            TargetMassKg = targetMassKg < 0f ? 0f : targetMassKg;
            HitZoneImpulseScale = hitZoneImpulseScale < 0f ? 0f : hitZoneImpulseScale;
            MassTransfer = massTransfer < 0f ? 0f : massTransfer;
            EffectiveImpact = effectiveImpact < 0f ? 0f : effectiveImpact;
            ScatterImpact = scatterImpact < 0 ? 0 : scatterImpact;
            HitCount = hitCount < 0 ? 0 : hitCount;
            HitCountToBreak = hitCountToBreak < 0 ? 0 : hitCountToBreak;
            ScatterPressureBefore = scatterPressureBefore < 0 ? 0 : scatterPressureBefore;
            ScatterPressureAfterFirstHit = scatterPressureAfterFirstHit < 0 ? 0 : scatterPressureAfterFirstHit;
            ScatterPressureAfterInputs = scatterPressureAfterInputs < 0 ? 0 : scatterPressureAfterInputs;
            ScatterPressureBeforeRecovery = scatterPressureBeforeRecovery < 0 ? 0 : scatterPressureBeforeRecovery;
            ScatterPressureAfterRecovery = scatterPressureAfterRecovery < 0 ? 0 : scatterPressureAfterRecovery;
            CohesionLimitBefore = cohesionLimitBefore <= 0 ? 1 : cohesionLimitBefore;
            CohesionLimit = cohesionLimit <= 0 ? 1 : cohesionLimit;
            MaxScatterPressure = maxScatterPressure <= 0 ? 1 : maxScatterPressure;
            CohesionSupportActive = cohesionSupportActive;
            CohesionSupportWindowActive = cohesionSupportWindowActive;
            CohesionSupportRate = cohesionSupportRate < 0f ? 0f : cohesionSupportRate;
            SupportLocalFrame = supportLocalFrame < 0 ? 0 : supportLocalFrame;
            BandAfterFirstHit = bandAfterFirstHit;
            BandAfterInputs = bandAfterInputs;
            BandAfterRecovery = bandAfterRecovery;
            PressureChangedEventCount = pressureChangedEventCount < 0 ? 0 : pressureChangedEventCount;
            ScatterImpactResolvedEventCount = scatterImpactResolvedEventCount < 0 ? 0 : scatterImpactResolvedEventCount;
            PressureBandChangedEventCount = pressureBandChangedEventCount < 0 ? 0 : pressureBandChangedEventCount;
            CohesionBreakEventCount = cohesionBreakEventCount < 0 ? 0 : cohesionBreakEventCount;
            RecoveryEventCount = recoveryEventCount < 0 ? 0 : recoveryEventCount;
            FirstCohesionBreakFrame = firstCohesionBreakFrame;
            RecoveryDelayFrames = recoveryDelayFrames < 0 ? 0 : recoveryDelayFrames;
            RecoveryRate = recoveryRate < 0 ? 0 : recoveryRate;
            RecoveryTickCount = recoveryTickCount < 0 ? 0 : recoveryTickCount;
            PreviewKnockbackDistanceMeters = previewKnockbackDistanceMeters < 0f ? 0f : previewKnockbackDistanceMeters;
            KnockbackSpeed = knockbackSpeed < 0f ? 0f : knockbackSpeed;
            LaterHook = laterHook ?? string.Empty;
        }

        public CharacterTestGenesisCombatPotencyReceptionKind Kind { get; }
        public string SampleId { get; }
        public string StateModel { get; }
        public int InputD { get; }
        public int InputS { get; }
        public bool IsContinuousInput { get; }
        public int ImpactPower { get; }
        public float SourceMassKg { get; }
        public float TargetMassKg { get; }
        public float HitZoneImpulseScale { get; }
        public float MassTransfer { get; }
        public float EffectiveImpact { get; }
        public int ScatterImpact { get; }
        public int HitCount { get; }
        public int HitCountToBreak { get; }
        public int ScatterPressureBefore { get; }
        public int ScatterPressureAfterFirstHit { get; }
        public int ScatterPressureAfterInputs { get; }
        public int ScatterPressureBeforeRecovery { get; }
        public int ScatterPressureAfterRecovery { get; }
        public int CohesionLimitBefore { get; }
        public int CohesionLimit { get; }
        public int MaxScatterPressure { get; }
        public bool CohesionSupportActive { get; }
        public bool CohesionSupportWindowActive { get; }
        public float CohesionSupportRate { get; }
        public int SupportLocalFrame { get; }
        public PressureBand BandAfterFirstHit { get; }
        public PressureBand BandAfterInputs { get; }
        public PressureBand BandAfterRecovery { get; }
        public int PressureChangedEventCount { get; }
        public int ScatterImpactResolvedEventCount { get; }
        public int PressureBandChangedEventCount { get; }
        public int CohesionBreakEventCount { get; }
        public int RecoveryEventCount { get; }
        public long FirstCohesionBreakFrame { get; }
        public int RecoveryDelayFrames { get; }
        public int RecoveryRate { get; }
        public int RecoveryTickCount { get; }
        public float PreviewKnockbackDistanceMeters { get; }
        public float KnockbackSpeed { get; }
        public string LaterHook { get; }
        public bool TriggeredCohesionBreak => CohesionBreakEventCount > 0;
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);
        public int RecoveryAmount => Math.Max(0, ScatterPressureBeforeRecovery - ScatterPressureAfterRecovery);
        public int TotalScatterInput => ScatterImpact * HitCount;
        public int CohesionLimitDelta => CohesionLimit - CohesionLimitBefore;
        public float CohesionBreakRiskAfterInputs => CohesionLimit <= 0 ? 0f : (float)ScatterPressureAfterInputs / CohesionLimit;
        public float CohesionBreakRiskAfterRecovery => CohesionLimit <= 0 ? 0f : (float)ScatterPressureAfterRecovery / CohesionLimit;

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "sampleId={0} kind={1} stateModel={2} inputD={3} inputS={4} continuous={5} impactPower={6} sourceMassKg={7:0.###} targetMassKg={8:0.###} hitZoneScale={9:0.###} massTransfer={10:0.###} effectiveImpact={11:0.###} runtime.ScatterImpact=scatterInput:{12} hits={13} totalScatterInput={14} runtime.ScatterPressure=scatterAccumulation:{15}->{16}->{17} cohesionBreakRisk={18:0.###}->{19:0.###} runtime.CohesionLimit=cohesionLimit:{20} maxScatterPressure={21} runtime.CohesionSupport=cohesionSupport:{22} supportRate={23:0.###} runtime.CohesionSupportWindow=cohesionSupport:{24} localFrame={25} bandFirst={26} bandInput={27} bandRecovery={28} runtime.PressureBandChangedEvent=scatterPhaseChange:{29} runtime.CohesionBreak=breakCohesion:{30} hitCountToBreak={31} firstBreakFrame={32} runtime.Recovery=recoverCohesion:delay={33},rate={34},ticks={35},amount={36} pressureEvents={37} scatterImpactEvents={38} recoveryEvents={39} knockbackDistance={40:0.###} knockbackSpeed={41:0.###} laterHook={42}",
                SampleId,
                Kind,
                StateModel,
                InputD,
                InputS,
                IsContinuousInput,
                ImpactPower,
                SourceMassKg,
                TargetMassKg,
                HitZoneImpulseScale,
                MassTransfer,
                EffectiveImpact,
                ScatterImpact,
                HitCount,
                TotalScatterInput,
                ScatterPressureBefore,
                ScatterPressureAfterInputs,
                ScatterPressureAfterRecovery,
                CohesionBreakRiskAfterInputs,
                CohesionBreakRiskAfterRecovery,
                CohesionLimit,
                MaxScatterPressure,
                CohesionSupportActive,
                CohesionSupportRate,
                CohesionSupportWindowActive,
                SupportLocalFrame,
                BandAfterFirstHit,
                BandAfterInputs,
                BandAfterRecovery,
                PressureBandChangedEventCount,
                TriggeredCohesionBreak,
                HitCountToBreak,
                FirstCohesionBreakFrame,
                RecoveryDelayFrames,
                RecoveryRate,
                RecoveryTickCount,
                RecoveryAmount,
                PressureChangedEventCount,
                ScatterImpactResolvedEventCount,
                RecoveryEventCount,
                PreviewKnockbackDistanceMeters,
                KnockbackSpeed,
                LaterHook);
        }
    }
}
