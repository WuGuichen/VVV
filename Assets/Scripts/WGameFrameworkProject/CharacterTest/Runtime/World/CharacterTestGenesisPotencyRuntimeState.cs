using System;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestGenesisPotencyResultSource
    {
        None = 0,
        FireBurning = 1,
        HammerCrush = 2,
        HammerCrack = 3,
        CondenseLayer = 4,
        ThresholdNotMet = 5
    }


    public readonly struct CharacterTestGenesisPotencyProfile : IEquatable<CharacterTestGenesisPotencyProfile>
    {
        public const int DefaultBurningMinMotion = 15;
        public const int DefaultBurningMinScatter = 18;
        public const int DefaultBurningMaxCohesion = 25;
        public const int DefaultCondensedLayerMinCohesion = 40;
        public const int DefaultCondensedLayerMaxMotion = 4;

        public static readonly CharacterTestGenesisPotencyProfile Disabled =
            new CharacterTestGenesisPotencyProfile(
                immuneMotion: false,
                immuneCohesion: false,
                immuneScatter: false,
                burningEnabled: false,
                crushEnabled: false,
                crackEnabled: false,
                condensedLayerEnabled: false,
                burningMinMotion: 0,
                burningMinScatter: 0,
                burningMaxCohesion: 0,
                condensedLayerMinCohesion: 0,
                condensedLayerMaxMotion: 0);

        public CharacterTestGenesisPotencyProfile(
            bool immuneMotion,
            bool immuneCohesion,
            bool immuneScatter,
            bool burningEnabled,
            bool crushEnabled,
            bool crackEnabled,
            bool condensedLayerEnabled,
            int burningMinMotion,
            int burningMinScatter,
            int burningMaxCohesion,
            int condensedLayerMinCohesion,
            int condensedLayerMaxMotion)
        {
            ImmuneMotion = immuneMotion;
            ImmuneCohesion = immuneCohesion;
            ImmuneScatter = immuneScatter;
            BurningEnabled = burningEnabled;
            CrushEnabled = crushEnabled;
            CrackEnabled = crackEnabled;
            CondensedLayerEnabled = condensedLayerEnabled;
            BurningMinMotion = CharacterTestGenesisPotencyRuntime.ClampStateValue(burningMinMotion);
            BurningMinScatter = CharacterTestGenesisPotencyRuntime.ClampStateValue(burningMinScatter);
            BurningMaxCohesion = CharacterTestGenesisPotencyRuntime.ClampStateValue(burningMaxCohesion);
            CondensedLayerMinCohesion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(condensedLayerMinCohesion);
            CondensedLayerMaxMotion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(condensedLayerMaxMotion);
        }

        public bool ImmuneMotion { get; }
        public bool ImmuneCohesion { get; }
        public bool ImmuneScatter { get; }
        public bool BurningEnabled { get; }
        public bool CrushEnabled { get; }
        public bool CrackEnabled { get; }
        public bool CondensedLayerEnabled { get; }
        public int BurningMinMotion { get; }
        public int BurningMinScatter { get; }
        public int BurningMaxCohesion { get; }
        public int CondensedLayerMinCohesion { get; }
        public int CondensedLayerMaxMotion { get; }

        public static CharacterTestGenesisPotencyProfile WithDefaultThresholds(
            bool immuneMotion,
            bool immuneCohesion,
            bool immuneScatter,
            bool burningEnabled,
            bool crushEnabled,
            bool crackEnabled,
            bool condensedLayerEnabled)
        {
            return new CharacterTestGenesisPotencyProfile(
                immuneMotion,
                immuneCohesion,
                immuneScatter,
                burningEnabled,
                crushEnabled,
                crackEnabled,
                condensedLayerEnabled,
                DefaultBurningMinMotion,
                DefaultBurningMinScatter,
                DefaultBurningMaxCohesion,
                DefaultCondensedLayerMinCohesion,
                DefaultCondensedLayerMaxMotion);
        }

        public CharacterTestGenesisPotencyProfile Normalize()
        {
            return new CharacterTestGenesisPotencyProfile(
                ImmuneMotion,
                ImmuneCohesion,
                ImmuneScatter,
                BurningEnabled,
                CrushEnabled,
                CrackEnabled,
                CondensedLayerEnabled,
                BurningMinMotion,
                BurningMinScatter,
                BurningMaxCohesion,
                CondensedLayerMinCohesion,
                CondensedLayerMaxMotion);
        }

        public bool Equals(CharacterTestGenesisPotencyProfile other)
        {
            return ImmuneMotion == other.ImmuneMotion
                && ImmuneCohesion == other.ImmuneCohesion
                && ImmuneScatter == other.ImmuneScatter
                && BurningEnabled == other.BurningEnabled
                && CrushEnabled == other.CrushEnabled
                && CrackEnabled == other.CrackEnabled
                && CondensedLayerEnabled == other.CondensedLayerEnabled
                && BurningMinMotion == other.BurningMinMotion
                && BurningMinScatter == other.BurningMinScatter
                && BurningMaxCohesion == other.BurningMaxCohesion
                && CondensedLayerMinCohesion == other.CondensedLayerMinCohesion
                && CondensedLayerMaxMotion == other.CondensedLayerMaxMotion;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestGenesisPotencyProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ImmuneMotion ? 1 : 0;
                hash = (hash * 397) ^ (ImmuneCohesion ? 1 : 0);
                hash = (hash * 397) ^ (ImmuneScatter ? 1 : 0);
                hash = (hash * 397) ^ (BurningEnabled ? 1 : 0);
                hash = (hash * 397) ^ (CrushEnabled ? 1 : 0);
                hash = (hash * 397) ^ (CrackEnabled ? 1 : 0);
                hash = (hash * 397) ^ (CondensedLayerEnabled ? 1 : 0);
                hash = (hash * 397) ^ BurningMinMotion;
                hash = (hash * 397) ^ BurningMinScatter;
                hash = (hash * 397) ^ BurningMaxCohesion;
                hash = (hash * 397) ^ CondensedLayerMinCohesion;
                hash = (hash * 397) ^ CondensedLayerMaxMotion;
                return hash;
            }
        }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "immune=M:{0},C:{1},S:{2} enabled=burn:{3},crush:{4},crack:{5},condense:{6} burn>=({7},{8}) cohesion<={9} condenseC>={10} motion<={11}",
                ImmuneMotion ? "1" : "0",
                ImmuneCohesion ? "1" : "0",
                ImmuneScatter ? "1" : "0",
                BurningEnabled ? "1" : "0",
                CrushEnabled ? "1" : "0",
                CrackEnabled ? "1" : "0",
                CondensedLayerEnabled ? "1" : "0",
                BurningMinMotion,
                BurningMinScatter,
                BurningMaxCohesion,
                CondensedLayerMinCohesion,
                CondensedLayerMaxMotion);
        }
    }

    public readonly struct CharacterTestGenesisPotencyApplicationRecord
    {
        public static readonly CharacterTestGenesisPotencyApplicationRecord None =
            new CharacterTestGenesisPotencyApplicationRecord(
                RuntimeFrame.Zero,
                0,
                CharacterTestGenesisPotencyInfluenceKind.None,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                immuneMotion: false,
                immuneCohesion: false,
                immuneScatter: false,
                burningThresholdMet: false,
                crushThresholdMet: false,
                crackThresholdMet: false,
                condensedLayerThresholdMet: false,
                CharacterTestGenesisPotencyResultKind.None,
                CharacterTestGenesisPotencyResultSource.None);

        public CharacterTestGenesisPotencyApplicationRecord(
            RuntimeFrame frame,
            int targetNumericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            CharacterTestGenesisPotencyTriplet before,
            CharacterTestGenesisPotencyTriplet rawDelta,
            CharacterTestGenesisPotencyTriplet appliedDelta,
            CharacterTestGenesisPotencyTriplet ignoredDelta,
            CharacterTestGenesisPotencyTriplet after,
            bool immuneMotion,
            bool immuneCohesion,
            bool immuneScatter,
            bool burningThresholdMet,
            bool crushThresholdMet,
            bool crackThresholdMet,
            bool condensedLayerThresholdMet,
            CharacterTestGenesisPotencyResultKind result,
            CharacterTestGenesisPotencyResultSource resultSource)
        {
            Frame = frame;
            TargetNumericId = targetNumericId > 0 ? targetNumericId : 0;
            InfluenceKind = CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(influenceKind);
            Before = before;
            RawDelta = rawDelta;
            AppliedDelta = appliedDelta;
            IgnoredDelta = ignoredDelta;
            After = after;
            ImmuneMotion = immuneMotion;
            ImmuneCohesion = immuneCohesion;
            ImmuneScatter = immuneScatter;
            BurningThresholdMet = burningThresholdMet;
            CrushThresholdMet = crushThresholdMet;
            CrackThresholdMet = crackThresholdMet;
            CondensedLayerThresholdMet = condensedLayerThresholdMet;
            Result = NormalizeResultKind(result);
            if ((uint)resultSource > (uint)CharacterTestGenesisPotencyResultSource.ThresholdNotMet)
                throw new ArgumentOutOfRangeException(nameof(resultSource));
            ResultSourceCode = resultSource;
        }

        public RuntimeFrame Frame { get; }
        public int TargetNumericId { get; }
        public CharacterTestGenesisPotencyInfluenceKind InfluenceKind { get; }
        public CharacterTestGenesisPotencyTriplet Before { get; }
        public CharacterTestGenesisPotencyTriplet RawDelta { get; }
        public CharacterTestGenesisPotencyTriplet AppliedDelta { get; }
        public CharacterTestGenesisPotencyTriplet IgnoredDelta { get; }
        public CharacterTestGenesisPotencyTriplet After { get; }
        public bool ImmuneMotion { get; }
        public bool ImmuneCohesion { get; }
        public bool ImmuneScatter { get; }
        public bool BurningThresholdMet { get; }
        public bool CrushThresholdMet { get; }
        public bool CrackThresholdMet { get; }
        public bool CondensedLayerThresholdMet { get; }
        public CharacterTestGenesisPotencyResultKind Result { get; }
        public CharacterTestGenesisPotencyResultSource ResultSourceCode { get; }
        public string ResultSource => ResultSourceText(ResultSourceCode);

        public static string ResultSourceText(CharacterTestGenesisPotencyResultSource code)
        {
            switch (code)
            {
                case CharacterTestGenesisPotencyResultSource.FireBurning: return "fire.burning";
                case CharacterTestGenesisPotencyResultSource.HammerCrush: return "hammer.crush";
                case CharacterTestGenesisPotencyResultSource.HammerCrack: return "hammer.crack";
                case CharacterTestGenesisPotencyResultSource.CondenseLayer: return "condense.layer";
                case CharacterTestGenesisPotencyResultSource.ThresholdNotMet: return "threshold-not-met";
                default: return "none";
            }
        }

        public string ToSummaryLine()
        {
            return "frame="
                + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " target="
                + TargetNumericId.ToString(CultureInfo.InvariantCulture)
                + " influence="
                + InfluenceKind
                + " beforeDPS="
                + Before.ToTupleString()
                + " rawDelta="
                + RawDelta.ToTupleString()
                + " appliedDelta="
                + AppliedDelta.ToTupleString()
                + " ignoredDelta="
                + IgnoredDelta.ToTupleString()
                + " afterDPS="
                + After.ToTupleString()
                + " immunity=M:"
                + (ImmuneMotion ? "1" : "0")
                + ",C:"
                + (ImmuneCohesion ? "1" : "0")
                + ",S:"
                + (ImmuneScatter ? "1" : "0")
                + " thresholds=burning:"
                + (BurningThresholdMet ? "true" : "false")
                + ",crush:"
                + (CrushThresholdMet ? "true" : "false")
                + ",crack:"
                + (CrackThresholdMet ? "true" : "false")
                + ",condense:"
                + (CondensedLayerThresholdMet ? "true" : "false")
                + " result="
                + Result
                + " resultSource="
                + ResultSource;
        }

        private static CharacterTestGenesisPotencyResultKind NormalizeResultKind(
            CharacterTestGenesisPotencyResultKind result)
        {
            switch (result)
            {
                case CharacterTestGenesisPotencyResultKind.Burning:
                case CharacterTestGenesisPotencyResultKind.CrushedOrBroken:
                case CharacterTestGenesisPotencyResultKind.Cracked:
                case CharacterTestGenesisPotencyResultKind.CondensedLayer:
                    return result;
                default:
                    return CharacterTestGenesisPotencyResultKind.None;
            }
        }
    }

    public static class CharacterTestGenesisPotencyRuntime
    {
        public const int MinStateValue = 0;
        public const int MaxStateValue = 1000;

        public static CharacterTestGenesisPotencyTriplet ClampState(
            CharacterTestGenesisPotencyTriplet value)
        {
            return new CharacterTestGenesisPotencyTriplet(
                ClampStateValue(value.Motion),
                ClampStateValue(value.Cohesion),
                ClampStateValue(value.Scatter));
        }

        public static int ClampStateValue(int value)
        {
            if (value < MinStateValue)
                return MinStateValue;
            if (value > MaxStateValue)
                return MaxStateValue;
            return value;
        }

        public static CharacterTestGenesisPotencyInfluenceKind NormalizeInfluenceKind(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            switch (influenceKind)
            {
                case CharacterTestGenesisPotencyInfluenceKind.Fire:
                case CharacterTestGenesisPotencyInfluenceKind.Hammer:
                case CharacterTestGenesisPotencyInfluenceKind.Condense:
                    return influenceKind;
                default:
                    return CharacterTestGenesisPotencyInfluenceKind.None;
            }
        }

        public static bool IsCommandInfluenceKind(CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            return NormalizeInfluenceKind(influenceKind) != CharacterTestGenesisPotencyInfluenceKind.None;
        }

        public static CharacterTestGenesisPotencyTriplet CreateRawDelta(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            switch (NormalizeInfluenceKind(influenceKind))
            {
                case CharacterTestGenesisPotencyInfluenceKind.Fire:
                    return new CharacterTestGenesisPotencyTriplet(18, 0, 22);
                case CharacterTestGenesisPotencyInfluenceKind.Hammer:
                    return new CharacterTestGenesisPotencyTriplet(70, -50, 6);
                case CharacterTestGenesisPotencyInfluenceKind.Condense:
                    return new CharacterTestGenesisPotencyTriplet(-2, 24, 0);
                default:
                    return CharacterTestGenesisPotencyTriplet.Zero;
            }
        }

        public static CharacterTestGenesisPotencyApplicationRecord Apply(
            CharacterTestGenesisPotencyTriplet current,
            CharacterTestGenesisPotencyProfile profile,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            RuntimeFrame frame,
            int targetNumericId,
            out CharacterTestGenesisPotencyTriplet after)
        {
            CharacterTestGenesisPotencyInfluenceKind normalizedInfluence =
                NormalizeInfluenceKind(influenceKind);
            CharacterTestGenesisPotencyTriplet before = ClampState(current);
            CharacterTestGenesisPotencyTriplet rawDelta = CreateRawDelta(normalizedInfluence);
            CharacterTestGenesisPotencyProfile normalizedProfile = profile.Normalize();
            CharacterTestGenesisPotencyTriplet acceptedDelta = new CharacterTestGenesisPotencyTriplet(
                normalizedProfile.ImmuneMotion ? 0 : rawDelta.Motion,
                normalizedProfile.ImmuneCohesion ? 0 : rawDelta.Cohesion,
                normalizedProfile.ImmuneScatter ? 0 : rawDelta.Scatter);

            after = ClampState(before + acceptedDelta);
            CharacterTestGenesisPotencyTriplet appliedDelta = after - before;
            CharacterTestGenesisPotencyTriplet ignoredDelta = rawDelta - appliedDelta;

            bool burningThresholdMet =
                normalizedProfile.BurningEnabled
                && after.Motion >= normalizedProfile.BurningMinMotion
                && after.Scatter >= normalizedProfile.BurningMinScatter
                && after.Cohesion <= normalizedProfile.BurningMaxCohesion;
            bool crushThresholdMet =
                normalizedProfile.CrushEnabled
                && after.Motion > after.Cohesion;
            bool crackThresholdMet =
                normalizedProfile.CrackEnabled
                && after.Motion > after.Cohesion;
            bool condensedLayerThresholdMet =
                normalizedProfile.CondensedLayerEnabled
                && after.Cohesion >= normalizedProfile.CondensedLayerMinCohesion
                && after.Motion <= normalizedProfile.CondensedLayerMaxMotion;

            CharacterTestGenesisPotencyResultKind result =
                ResolveResult(
                    normalizedInfluence,
                    burningThresholdMet,
                    crushThresholdMet,
                    crackThresholdMet,
                    condensedLayerThresholdMet,
                    out CharacterTestGenesisPotencyResultSource resultSource);

            return new CharacterTestGenesisPotencyApplicationRecord(
                frame,
                targetNumericId,
                normalizedInfluence,
                before,
                rawDelta,
                appliedDelta,
                ignoredDelta,
                after,
                normalizedProfile.ImmuneMotion,
                normalizedProfile.ImmuneCohesion,
                normalizedProfile.ImmuneScatter,
                burningThresholdMet,
                crushThresholdMet,
                crackThresholdMet,
                condensedLayerThresholdMet,
                result,
                resultSource);
        }

        private static CharacterTestGenesisPotencyResultKind ResolveResult(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            bool burningThresholdMet,
            bool crushThresholdMet,
            bool crackThresholdMet,
            bool condensedLayerThresholdMet,
            out CharacterTestGenesisPotencyResultSource resultSource)
        {
            switch (influenceKind)
            {
                case CharacterTestGenesisPotencyInfluenceKind.Fire:
                    if (burningThresholdMet)
                    {
                        resultSource = CharacterTestGenesisPotencyResultSource.FireBurning;
                        return CharacterTestGenesisPotencyResultKind.Burning;
                    }

                    break;
                case CharacterTestGenesisPotencyInfluenceKind.Hammer:
                    if (crushThresholdMet)
                    {
                        resultSource = CharacterTestGenesisPotencyResultSource.HammerCrush;
                        return CharacterTestGenesisPotencyResultKind.CrushedOrBroken;
                    }

                    if (crackThresholdMet)
                    {
                        resultSource = CharacterTestGenesisPotencyResultSource.HammerCrack;
                        return CharacterTestGenesisPotencyResultKind.Cracked;
                    }

                    break;
                case CharacterTestGenesisPotencyInfluenceKind.Condense:
                    if (condensedLayerThresholdMet)
                    {
                        resultSource = CharacterTestGenesisPotencyResultSource.CondenseLayer;
                        return CharacterTestGenesisPotencyResultKind.CondensedLayer;
                    }

                    break;
            }

            resultSource = influenceKind == CharacterTestGenesisPotencyInfluenceKind.None
                ? CharacterTestGenesisPotencyResultSource.None
                : CharacterTestGenesisPotencyResultSource.ThresholdNotMet;
            return CharacterTestGenesisPotencyResultKind.None;
        }
    }
}


