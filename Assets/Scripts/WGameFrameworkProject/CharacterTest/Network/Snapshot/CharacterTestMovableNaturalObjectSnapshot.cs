using System;
using System.Globalization;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestMovableNaturalObjectSnapshot
    {
        public string StableId { get; set; }
        public int NumericId { get; set; }
        public string Category { get; set; }
        public int MassGrams { get; set; }
        public int MaterialKind { get; set; }
        public string MaterialStableId { get; set; }
        public int MaterialFrictionMilli { get; set; }
        public int MaterialElasticityMilli { get; set; }
        public int MaterialFixedBounceHeightMillimeters { get; set; }
        public int ShapeKind { get; set; }
        public int HalfExtentXMillimeters { get; set; }
        public int HalfExtentYMillimeters { get; set; }
        public int HalfExtentZMillimeters { get; set; }
        public int XMillimeters { get; set; }
        public int YMillimeters { get; set; }
        public int ZMillimeters { get; set; }
        public int VelocityXMillimetersPerSecond { get; set; }
        public int VelocityYMillimetersPerSecond { get; set; }
        public int VelocityZMillimetersPerSecond { get; set; }
        public int ContactVelocityXMillimetersPerSecond { get; set; }
        public int ContactVelocityZMillimetersPerSecond { get; set; }
        public int ContactDisplacementXMillimeters { get; set; }
        public int ContactDisplacementZMillimeters { get; set; }
        public int ContactPressure { get; set; }
        public int ContactPairKind { get; set; }
        public int ContactAId { get; set; }
        public int ContactBId { get; set; }
        [Newtonsoft.Json.JsonIgnore] public string ContactPair => CharacterTestMovableNaturalObjectState.FormatContactPair((CharacterTestMovableNaturalObjectContactPairKind)ContactPairKind, ContactAId, ContactBId);
        public CharacterTestMovableObjectBehavior BehaviorState { get; set; }
        public int ContactCount { get; set; }
        public int BasePotencyMotion { get; set; }
        public int BasePotencyCohesion { get; set; }
        public int BasePotencyScatter { get; set; }
        public int CurrentPotencyMotion { get; set; }
        public int CurrentPotencyCohesion { get; set; }
        public int CurrentPotencyScatter { get; set; }
        public int PendingPotencyMotion { get; set; }
        public int PendingPotencyCohesion { get; set; }
        public int PendingPotencyScatter { get; set; }
        public int PendingPotencyInfluenceKind { get; set; }
        public bool PotencyImmuneMotion { get; set; }
        public bool PotencyImmuneCohesion { get; set; }
        public bool PotencyImmuneScatter { get; set; }
        public bool PotencyBurningEnabled { get; set; }
        public bool PotencyCrushEnabled { get; set; }
        public bool PotencyCrackEnabled { get; set; }
        public bool PotencyCondensedLayerEnabled { get; set; }
        public int PotencyBurningMinMotion { get; set; }
        public int PotencyBurningMinScatter { get; set; }
        public int PotencyBurningMaxCohesion { get; set; }
        public int PotencyCondensedLayerMinCohesion { get; set; }
        public int PotencyCondensedLayerMaxMotion { get; set; }
        public long LatestPotencyFrame { get; set; }
        public int LatestPotencyTargetNumericId { get; set; }
        public int LatestPotencyInfluenceKind { get; set; }
        public int LatestPotencyBeforeMotion { get; set; }
        public int LatestPotencyBeforeCohesion { get; set; }
        public int LatestPotencyBeforeScatter { get; set; }
        public int LatestPotencyRawDeltaMotion { get; set; }
        public int LatestPotencyRawDeltaCohesion { get; set; }
        public int LatestPotencyRawDeltaScatter { get; set; }
        public int LatestPotencyAppliedDeltaMotion { get; set; }
        public int LatestPotencyAppliedDeltaCohesion { get; set; }
        public int LatestPotencyAppliedDeltaScatter { get; set; }
        public int LatestPotencyIgnoredDeltaMotion { get; set; }
        public int LatestPotencyIgnoredDeltaCohesion { get; set; }
        public int LatestPotencyIgnoredDeltaScatter { get; set; }
        public int LatestPotencyAfterMotion { get; set; }
        public int LatestPotencyAfterCohesion { get; set; }
        public int LatestPotencyAfterScatter { get; set; }
        public bool LatestPotencyImmuneMotion { get; set; }
        public bool LatestPotencyImmuneCohesion { get; set; }
        public bool LatestPotencyImmuneScatter { get; set; }
        public bool LatestPotencyBurningThresholdMet { get; set; }
        public bool LatestPotencyCrushThresholdMet { get; set; }
        public bool LatestPotencyCrackThresholdMet { get; set; }
        public bool LatestPotencyCondensedLayerThresholdMet { get; set; }
        public int LatestPotencyResultKind { get; set; }
        public CharacterTestGenesisPotencyResultSource LatestPotencyResultSource { get; set; }

        public CharacterTestMovableNaturalObjectSnapshot CloneForRollback()
        {
            return (CharacterTestMovableNaturalObjectSnapshot)MemberwiseClone();
        }

        public CombatSurfaceMaterial CreateSurfaceMaterial()
        {
            return new CombatSurfaceMaterial(
                ResolveSurfaceMaterialKind(MaterialKind, MaterialStableId),
                ToFix64(CharacterTestDeterministicUnits.MilliToScale(MaterialFrictionMilli)),
                ToFix64(CharacterTestDeterministicUnits.MilliToScale(MaterialElasticityMilli)),
                ToFix64(CharacterTestDeterministicUnits.MillimetersToMeters(
                    MaterialFixedBounceHeightMillimeters)));
        }

        public string ToSummaryLine(int index)
        {
            return "object."
                + index.ToString(CultureInfo.InvariantCulture)
                + " stableId="
                + (StableId ?? string.Empty)
                + " numericId="
                + NumericId.ToString(CultureInfo.InvariantCulture)
                + " category="
                + (Category ?? string.Empty)
                + " massKg="
                + CharacterTestDeterministicUnits.GramsToKilograms(MassGrams).ToString("0.###", CultureInfo.InvariantCulture)
                + " material="
                + ((CombatSurfaceMaterialKind)MaterialKind)
                + "/"
                + (MaterialStableId ?? string.Empty)
                + " shape="
                + ((CharacterTestMovableNaturalObjectShapeKind)ShapeKind)
                + " halfExtentsMm=("
                + HalfExtentXMillimeters.ToString(CultureInfo.InvariantCulture)
                + ","
                + HalfExtentYMillimeters.ToString(CultureInfo.InvariantCulture)
                + ","
                + HalfExtentZMillimeters.ToString(CultureInfo.InvariantCulture)
                + ") positionMm=("
                + XMillimeters.ToString(CultureInfo.InvariantCulture)
                + ","
                + YMillimeters.ToString(CultureInfo.InvariantCulture)
                + ","
                + ZMillimeters.ToString(CultureInfo.InvariantCulture)
                + ") velocityMmS=("
                + VelocityXMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ","
                + VelocityYMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ","
                + VelocityZMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ") contactVelocityMmS=("
                + ContactVelocityXMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ","
                + ContactVelocityZMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ") contactDisplacementMm=("
                + ContactDisplacementXMillimeters.ToString(CultureInfo.InvariantCulture)
                + ","
                + ContactDisplacementZMillimeters.ToString(CultureInfo.InvariantCulture)
                + ") contactPressure="
                + ContactPressure.ToString(CultureInfo.InvariantCulture)
                + " contactPair="
                + (ContactPair ?? string.Empty)
                + " currentDPS="
                + CreateCurrentPotency().ToTupleString()
                + " baseDPS="
                + CreateBasePotency().ToTupleString()
                + " latestPotency=["
                + CreateLatestPotencyApplicationRecord().ToSummaryLine()
                + "]"
                + " behavior="
                + CharacterTestMovableNaturalObjectState.BehaviorStateText(BehaviorState);
        }

        public static CharacterTestMovableNaturalObjectSnapshot[] CloneArray(
            CharacterTestMovableNaturalObjectSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestMovableNaturalObjectSnapshot>();

            var result = new CharacterTestMovableNaturalObjectSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public static CharacterTestMovableNaturalObjectSnapshot FromState(
            CharacterTestMovableNaturalObjectState state)
        {
            if (state == null)
                return null;

            return new CharacterTestMovableNaturalObjectSnapshot
            {
                StableId = state.AuthoredStableLabel,
                NumericId = state.NumericId,
                Category = state.Category,
                MassGrams = state.MassGrams,
                MaterialKind = (int)state.Material.Kind,
                MaterialStableId = state.Material.StableId,
                MaterialFrictionMilli = CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Friction)),
                MaterialElasticityMilli = CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Elasticity)),
                MaterialFixedBounceHeightMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(ToFloat(state.Material.FixedBounceHeight)),
                ShapeKind = (int)state.ShapeKind,
                HalfExtentXMillimeters = state.HalfExtentXMillimeters,
                HalfExtentYMillimeters = state.HalfExtentYMillimeters,
                HalfExtentZMillimeters = state.HalfExtentZMillimeters,
                XMillimeters = state.XMillimeters,
                YMillimeters = state.YMillimeters,
                ZMillimeters = state.ZMillimeters,
                VelocityXMillimetersPerSecond = state.VelocityXMillimetersPerSecond,
                VelocityYMillimetersPerSecond = state.VelocityYMillimetersPerSecond,
                VelocityZMillimetersPerSecond = state.VelocityZMillimetersPerSecond,
                ContactVelocityXMillimetersPerSecond = state.ContactVelocityXMillimetersPerSecond,
                ContactVelocityZMillimetersPerSecond = state.ContactVelocityZMillimetersPerSecond,
                ContactDisplacementXMillimeters = state.ContactDisplacementXMillimeters,
                ContactDisplacementZMillimeters = state.ContactDisplacementZMillimeters,
                ContactPressure = state.ContactPressure,
                ContactPairKind = (int)state.ContactKind,
                ContactAId = state.ContactAId,
                ContactBId = state.ContactBId,
                BehaviorState = state.BehaviorStateCode,
                ContactCount = state.ContactCount,
                BasePotencyMotion = state.BasePotency.Motion,
                BasePotencyCohesion = state.BasePotency.Cohesion,
                BasePotencyScatter = state.BasePotency.Scatter,
                CurrentPotencyMotion = state.CurrentPotency.Motion,
                CurrentPotencyCohesion = state.CurrentPotency.Cohesion,
                CurrentPotencyScatter = state.CurrentPotency.Scatter,
                PendingPotencyMotion = state.PendingPotency.Motion,
                PendingPotencyCohesion = state.PendingPotency.Cohesion,
                PendingPotencyScatter = state.PendingPotency.Scatter,
                PendingPotencyInfluenceKind = (int)state.PendingPotencyInfluence,
                PotencyImmuneMotion = state.PotencyProfile.ImmuneMotion,
                PotencyImmuneCohesion = state.PotencyProfile.ImmuneCohesion,
                PotencyImmuneScatter = state.PotencyProfile.ImmuneScatter,
                PotencyBurningEnabled = state.PotencyProfile.BurningEnabled,
                PotencyCrushEnabled = state.PotencyProfile.CrushEnabled,
                PotencyCrackEnabled = state.PotencyProfile.CrackEnabled,
                PotencyCondensedLayerEnabled = state.PotencyProfile.CondensedLayerEnabled,
                PotencyBurningMinMotion = state.PotencyProfile.BurningMinMotion,
                PotencyBurningMinScatter = state.PotencyProfile.BurningMinScatter,
                PotencyBurningMaxCohesion = state.PotencyProfile.BurningMaxCohesion,
                PotencyCondensedLayerMinCohesion = state.PotencyProfile.CondensedLayerMinCohesion,
                PotencyCondensedLayerMaxMotion = state.PotencyProfile.CondensedLayerMaxMotion,
                LatestPotencyFrame = state.LatestPotencyApplication.Frame.Value,
                LatestPotencyTargetNumericId = state.LatestPotencyApplication.TargetNumericId,
                LatestPotencyInfluenceKind = (int)state.LatestPotencyApplication.InfluenceKind,
                LatestPotencyBeforeMotion = state.LatestPotencyApplication.Before.Motion,
                LatestPotencyBeforeCohesion = state.LatestPotencyApplication.Before.Cohesion,
                LatestPotencyBeforeScatter = state.LatestPotencyApplication.Before.Scatter,
                LatestPotencyRawDeltaMotion = state.LatestPotencyApplication.RawDelta.Motion,
                LatestPotencyRawDeltaCohesion = state.LatestPotencyApplication.RawDelta.Cohesion,
                LatestPotencyRawDeltaScatter = state.LatestPotencyApplication.RawDelta.Scatter,
                LatestPotencyAppliedDeltaMotion = state.LatestPotencyApplication.AppliedDelta.Motion,
                LatestPotencyAppliedDeltaCohesion = state.LatestPotencyApplication.AppliedDelta.Cohesion,
                LatestPotencyAppliedDeltaScatter = state.LatestPotencyApplication.AppliedDelta.Scatter,
                LatestPotencyIgnoredDeltaMotion = state.LatestPotencyApplication.IgnoredDelta.Motion,
                LatestPotencyIgnoredDeltaCohesion = state.LatestPotencyApplication.IgnoredDelta.Cohesion,
                LatestPotencyIgnoredDeltaScatter = state.LatestPotencyApplication.IgnoredDelta.Scatter,
                LatestPotencyAfterMotion = state.LatestPotencyApplication.After.Motion,
                LatestPotencyAfterCohesion = state.LatestPotencyApplication.After.Cohesion,
                LatestPotencyAfterScatter = state.LatestPotencyApplication.After.Scatter,
                LatestPotencyImmuneMotion = state.LatestPotencyApplication.ImmuneMotion,
                LatestPotencyImmuneCohesion = state.LatestPotencyApplication.ImmuneCohesion,
                LatestPotencyImmuneScatter = state.LatestPotencyApplication.ImmuneScatter,
                LatestPotencyBurningThresholdMet = state.LatestPotencyApplication.BurningThresholdMet,
                LatestPotencyCrushThresholdMet = state.LatestPotencyApplication.CrushThresholdMet,
                LatestPotencyCrackThresholdMet = state.LatestPotencyApplication.CrackThresholdMet,
                LatestPotencyCondensedLayerThresholdMet =
                    state.LatestPotencyApplication.CondensedLayerThresholdMet,
                LatestPotencyResultKind = (int)state.LatestPotencyApplication.Result,
                LatestPotencyResultSource = state.LatestPotencyApplication.ResultSourceCode
            };
        }

        public CharacterTestGenesisPotencyTriplet CreateBasePotency()
        {
            return CharacterTestGenesisPotencyRuntime.ClampState(new CharacterTestGenesisPotencyTriplet(
                BasePotencyMotion,
                BasePotencyCohesion,
                BasePotencyScatter));
        }

        public CharacterTestGenesisPotencyTriplet CreateCurrentPotency()
        {
            return CharacterTestGenesisPotencyRuntime.ClampState(new CharacterTestGenesisPotencyTriplet(
                CurrentPotencyMotion,
                CurrentPotencyCohesion,
                CurrentPotencyScatter));
        }

        public CharacterTestGenesisPotencyTriplet CreatePendingPotency()
        {
            return new CharacterTestGenesisPotencyTriplet(
                PendingPotencyMotion,
                PendingPotencyCohesion,
                PendingPotencyScatter);
        }

        public CharacterTestGenesisPotencyProfile CreatePotencyProfile()
        {
            return new CharacterTestGenesisPotencyProfile(
                PotencyImmuneMotion,
                PotencyImmuneCohesion,
                PotencyImmuneScatter,
                PotencyBurningEnabled,
                PotencyCrushEnabled,
                PotencyCrackEnabled,
                PotencyCondensedLayerEnabled,
                PotencyBurningMinMotion,
                PotencyBurningMinScatter,
                PotencyBurningMaxCohesion,
                PotencyCondensedLayerMinCohesion,
                PotencyCondensedLayerMaxMotion);
        }

        public CharacterTestGenesisPotencyApplicationRecord CreateLatestPotencyApplicationRecord()
        {
            return new CharacterTestGenesisPotencyApplicationRecord(
                new MxFramework.Runtime.RuntimeFrame(LatestPotencyFrame),
                LatestPotencyTargetNumericId,
                (CharacterTestGenesisPotencyInfluenceKind)LatestPotencyInfluenceKind,
                new CharacterTestGenesisPotencyTriplet(
                    LatestPotencyBeforeMotion,
                    LatestPotencyBeforeCohesion,
                    LatestPotencyBeforeScatter),
                new CharacterTestGenesisPotencyTriplet(
                    LatestPotencyRawDeltaMotion,
                    LatestPotencyRawDeltaCohesion,
                    LatestPotencyRawDeltaScatter),
                new CharacterTestGenesisPotencyTriplet(
                    LatestPotencyAppliedDeltaMotion,
                    LatestPotencyAppliedDeltaCohesion,
                    LatestPotencyAppliedDeltaScatter),
                new CharacterTestGenesisPotencyTriplet(
                    LatestPotencyIgnoredDeltaMotion,
                    LatestPotencyIgnoredDeltaCohesion,
                    LatestPotencyIgnoredDeltaScatter),
                new CharacterTestGenesisPotencyTriplet(
                    LatestPotencyAfterMotion,
                    LatestPotencyAfterCohesion,
                    LatestPotencyAfterScatter),
                LatestPotencyImmuneMotion,
                LatestPotencyImmuneCohesion,
                LatestPotencyImmuneScatter,
                LatestPotencyBurningThresholdMet,
                LatestPotencyCrushThresholdMet,
                LatestPotencyCrackThresholdMet,
                LatestPotencyCondensedLayerThresholdMet,
                (CharacterTestGenesisPotencyResultKind)LatestPotencyResultKind,
                LatestPotencyResultSource);
        }

        public void ResetPotencyForLegacySchema()
        {
            BasePotencyMotion = 0;
            BasePotencyCohesion = 0;
            BasePotencyScatter = 0;
            CurrentPotencyMotion = 0;
            CurrentPotencyCohesion = 0;
            CurrentPotencyScatter = 0;
            PendingPotencyMotion = 0;
            PendingPotencyCohesion = 0;
            PendingPotencyScatter = 0;
            PendingPotencyInfluenceKind = (int)CharacterTestGenesisPotencyInfluenceKind.None;
            PotencyImmuneMotion = false;
            PotencyImmuneCohesion = false;
            PotencyImmuneScatter = false;
            PotencyBurningEnabled = false;
            PotencyCrushEnabled = false;
            PotencyCrackEnabled = false;
            PotencyCondensedLayerEnabled = false;
            PotencyBurningMinMotion = 0;
            PotencyBurningMinScatter = 0;
            PotencyBurningMaxCohesion = 0;
            PotencyCondensedLayerMinCohesion = 0;
            PotencyCondensedLayerMaxMotion = 0;
            LatestPotencyFrame = 0;
            LatestPotencyTargetNumericId = 0;
            LatestPotencyInfluenceKind = (int)CharacterTestGenesisPotencyInfluenceKind.None;
            LatestPotencyBeforeMotion = 0;
            LatestPotencyBeforeCohesion = 0;
            LatestPotencyBeforeScatter = 0;
            LatestPotencyRawDeltaMotion = 0;
            LatestPotencyRawDeltaCohesion = 0;
            LatestPotencyRawDeltaScatter = 0;
            LatestPotencyAppliedDeltaMotion = 0;
            LatestPotencyAppliedDeltaCohesion = 0;
            LatestPotencyAppliedDeltaScatter = 0;
            LatestPotencyIgnoredDeltaMotion = 0;
            LatestPotencyIgnoredDeltaCohesion = 0;
            LatestPotencyIgnoredDeltaScatter = 0;
            LatestPotencyAfterMotion = 0;
            LatestPotencyAfterCohesion = 0;
            LatestPotencyAfterScatter = 0;
            LatestPotencyImmuneMotion = false;
            LatestPotencyImmuneCohesion = false;
            LatestPotencyImmuneScatter = false;
            LatestPotencyBurningThresholdMet = false;
            LatestPotencyCrushThresholdMet = false;
            LatestPotencyCrackThresholdMet = false;
            LatestPotencyCondensedLayerThresholdMet = false;
            LatestPotencyResultKind = (int)CharacterTestGenesisPotencyResultKind.None;
            LatestPotencyResultSource = CharacterTestGenesisPotencyResultSource.None;
        }

        private static CombatSurfaceMaterialKind ResolveSurfaceMaterialKind(int value, string stableId)
        {
            if (value == (int)CombatSurfaceMaterialKind.HardSlick)
                return CombatSurfaceMaterialKind.HardSlick;
            if (value == (int)CombatSurfaceMaterialKind.Rough)
                return CombatSurfaceMaterialKind.Rough;
            if (value == (int)CombatSurfaceMaterialKind.Elastic)
                return CombatSurfaceMaterialKind.Elastic;
            if (value == (int)CombatSurfaceMaterialKind.BouncePad)
                return CombatSurfaceMaterialKind.BouncePad;

            switch (stableId)
            {
                case "hard_slick":
                    return CombatSurfaceMaterialKind.HardSlick;
                case "rough":
                    return CombatSurfaceMaterialKind.Rough;
                case "elastic":
                    return CombatSurfaceMaterialKind.Elastic;
                case "bounce_pad":
                    return CombatSurfaceMaterialKind.BouncePad;
                default:
                    return CombatSurfaceMaterialKind.Default;
            }
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }
}


