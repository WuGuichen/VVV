using System;
using System.Globalization;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisCondensationProbeKind
    {
        None = 0,
        ValidationSuite = 1
    }

    internal enum CharacterTestGenesisCondensationResultKind
    {
        None = 0,
        CondensedLayer = 1
    }

    internal readonly struct CharacterTestGenesisCondensationValidation
    {
        public static readonly CharacterTestGenesisCondensationValidation Empty =
            new CharacterTestGenesisCondensationValidation(
                CharacterTestGenesisCondensationProbeKind.None,
                default,
                "harness-only",
                CharacterTestGenesisCondensationSample.Empty);

        public CharacterTestGenesisCondensationValidation(
            CharacterTestGenesisCondensationProbeKind scenario,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisCondensationSample sample)
        {
            Scenario = scenario;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            Sample = sample;
        }

        public CharacterTestGenesisCondensationProbeKind Scenario { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisCondensationSample Sample { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} scenario={1} frame={2} condensation=[{3}]",
                StateModel,
                Scenario,
                Frame.Value,
                Sample.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisCondensationSample
    {
        public static readonly CharacterTestGenesisCondensationSample Empty =
            new CharacterTestGenesisCondensationSample(
                string.Empty,
                0,
                CombatSurfaceMaterialKind.Default,
                "default",
                string.Empty,
                default,
                "harness-only",
                CharacterTestGenesisCondensationTriplet.Zero,
                CharacterTestGenesisCondensationTriplet.Zero,
                CharacterTestGenesisCondensationTriplet.Zero,
                CharacterTestGenesisCondensationTriplet.Zero,
                "none",
                thresholdMet: false,
                CharacterTestGenesisCondensationResultKind.None,
                "none",
                materialReplacement: false,
                sameContactInput: false,
                frictionBefore: 0f,
                layerFriction: 0f,
                frictionAfter: 0f,
                CharacterTestGenesisCondensationContactSample.Empty,
                CharacterTestGenesisCondensationContactSample.Empty);

        public CharacterTestGenesisCondensationSample(
            string surfaceSampleStableId,
            int surfaceSampleNumericId,
            CombatSurfaceMaterialKind baseMaterialKind,
            string baseMaterialStableId,
            string influenceStableId,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisCondensationTriplet before,
            CharacterTestGenesisCondensationTriplet rawDelta,
            CharacterTestGenesisCondensationTriplet appliedDelta,
            CharacterTestGenesisCondensationTriplet after,
            string layerThreshold,
            bool thresholdMet,
            CharacterTestGenesisCondensationResultKind result,
            string layerSource,
            bool materialReplacement,
            bool sameContactInput,
            float frictionBefore,
            float layerFriction,
            float frictionAfter,
            CharacterTestGenesisCondensationContactSample contactBefore,
            CharacterTestGenesisCondensationContactSample contactAfter)
        {
            SurfaceSampleStableId = surfaceSampleStableId ?? string.Empty;
            SurfaceSampleNumericId = surfaceSampleNumericId;
            BaseMaterialKind = baseMaterialKind;
            BaseMaterialStableId = baseMaterialStableId ?? string.Empty;
            InfluenceStableId = influenceStableId ?? string.Empty;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            Before = before;
            RawDelta = rawDelta;
            AppliedDelta = appliedDelta;
            After = after;
            LayerThreshold = string.IsNullOrWhiteSpace(layerThreshold) ? "none" : layerThreshold;
            ThresholdMet = thresholdMet;
            Result = result;
            LayerSource = string.IsNullOrWhiteSpace(layerSource) ? "none" : layerSource;
            MaterialReplacement = materialReplacement;
            SameContactInput = sameContactInput;
            FrictionBefore = frictionBefore < 0f ? 0f : frictionBefore;
            LayerFriction = layerFriction < 0f ? 0f : layerFriction;
            FrictionAfter = frictionAfter < 0f ? 0f : frictionAfter;
            ContactBefore = contactBefore;
            ContactAfter = contactAfter;
        }

        public string SurfaceSampleStableId { get; }
        public int SurfaceSampleNumericId { get; }
        public CombatSurfaceMaterialKind BaseMaterialKind { get; }
        public string BaseMaterialStableId { get; }
        public string InfluenceStableId { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisCondensationTriplet Before { get; }
        public CharacterTestGenesisCondensationTriplet RawDelta { get; }
        public CharacterTestGenesisCondensationTriplet AppliedDelta { get; }
        public CharacterTestGenesisCondensationTriplet After { get; }
        public string LayerThreshold { get; }
        public bool ThresholdMet { get; }
        public CharacterTestGenesisCondensationResultKind Result { get; }
        public string LayerSource { get; }
        public bool MaterialReplacement { get; }
        public bool SameContactInput { get; }
        public float FrictionBefore { get; }
        public float LayerFriction { get; }
        public float FrictionAfter { get; }
        public CharacterTestGenesisCondensationContactSample ContactBefore { get; }
        public CharacterTestGenesisCondensationContactSample ContactAfter { get; }
        public float SlideDistanceDelta => ContactBefore.SlideDistance - ContactAfter.SlideDistance;
        public float VelocityDecayDelta => ContactAfter.VelocityDecay - ContactBefore.VelocityDecay;
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "surfaceSample={0} numericId={1} baseMaterial={2}/{3} influence={4} frame={5} stateModel={6} beforeDPS={7} rawDelta={8} appliedDelta={9} afterDPS={10} layerThreshold={11} thresholdMet={12} result={13} layerSource={14} materialReplacement={15} sameContactInput={16} frictionBefore={17:0.###} layerFriction={18:0.###} frictionAfter={19:0.###} slideDistanceBefore={20:0.###} slideDistanceAfter={21:0.###} slideDistanceDelta={22:0.###} velocityDecayBefore={23:0.###} velocityDecayAfter={24:0.###} velocityDecayDelta={25:0.###} frictionReading=low-friction/longer-slide->higher-friction-layer/shorter-slide",
                SurfaceSampleStableId,
                SurfaceSampleNumericId,
                BaseMaterialKind,
                BaseMaterialStableId,
                InfluenceStableId,
                Frame.Value,
                StateModel,
                Before.ToTupleString(),
                RawDelta.ToTupleString(),
                AppliedDelta.ToTupleString(),
                After.ToTupleString(),
                LayerThreshold,
                ThresholdMet ? "true" : "false",
                Result,
                LayerSource,
                MaterialReplacement ? "true" : "false",
                SameContactInput ? "true" : "false",
                FrictionBefore,
                LayerFriction,
                FrictionAfter,
                ContactBefore.SlideDistance,
                ContactAfter.SlideDistance,
                SlideDistanceDelta,
                ContactBefore.VelocityDecay,
                ContactAfter.VelocityDecay,
                VelocityDecayDelta);
        }
    }

    internal readonly struct CharacterTestGenesisCondensationContactSample
    {
        public static readonly CharacterTestGenesisCondensationContactSample Empty =
            new CharacterTestGenesisCondensationContactSample(0f, 0, 0, 0f, 0f, 0f, 0f);

        public CharacterTestGenesisCondensationContactSample(
            float friction,
            int tickCount,
            int tickRate,
            float initialSpeed,
            float finalSpeed,
            float velocityDecay,
            float slideDistance)
        {
            Friction = friction < 0f ? 0f : friction;
            TickCount = tickCount < 0 ? 0 : tickCount;
            TickRate = tickRate < 0 ? 0 : tickRate;
            InitialSpeed = initialSpeed < 0f ? 0f : initialSpeed;
            FinalSpeed = finalSpeed < 0f ? 0f : finalSpeed;
            VelocityDecay = velocityDecay < 0f ? 0f : velocityDecay;
            SlideDistance = slideDistance < 0f ? 0f : slideDistance;
        }

        public float Friction { get; }
        public int TickCount { get; }
        public int TickRate { get; }
        public float InitialSpeed { get; }
        public float FinalSpeed { get; }
        public float VelocityDecay { get; }
        public float SlideDistance { get; }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "friction={0:0.###} ticks={1}@{2}Hz initialSpeed={3:0.###} finalSpeed={4:0.###} velocityDecay={5:0.###} slideDistance={6:0.###}",
                Friction,
                TickCount,
                TickRate,
                InitialSpeed,
                FinalSpeed,
                VelocityDecay,
                SlideDistance);
        }
    }

    internal readonly struct CharacterTestGenesisCondensationTriplet :
        IEquatable<CharacterTestGenesisCondensationTriplet>
    {
        public static readonly CharacterTestGenesisCondensationTriplet Zero =
            new CharacterTestGenesisCondensationTriplet(0, 0, 0);

        public CharacterTestGenesisCondensationTriplet(int d, int p, int s)
        {
            D = d;
            P = p;
            S = s;
        }

        public int D { get; }
        public int P { get; }
        public int S { get; }

        public static CharacterTestGenesisCondensationTriplet operator +(
            CharacterTestGenesisCondensationTriplet left,
            CharacterTestGenesisCondensationTriplet right)
        {
            return new CharacterTestGenesisCondensationTriplet(
                left.D + right.D,
                left.P + right.P,
                left.S + right.S);
        }

        public bool Equals(CharacterTestGenesisCondensationTriplet other)
        {
            return D == other.D && P == other.P && S == other.S;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestGenesisCondensationTriplet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = D;
                hash = (hash * 397) ^ P;
                hash = (hash * 397) ^ S;
                return hash;
            }
        }

        public string ToTupleString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0},{1},{2})", D, P, S);
        }

        public override string ToString()
        {
            return ToTupleString();
        }
    }
}
