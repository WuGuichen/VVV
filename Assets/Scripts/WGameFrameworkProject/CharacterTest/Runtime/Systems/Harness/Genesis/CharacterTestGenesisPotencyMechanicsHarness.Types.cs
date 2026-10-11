using System;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestGenesisPotencyMechanicsValidation
    {
        public static readonly CharacterTestGenesisPotencyMechanicsValidation Empty =
            new CharacterTestGenesisPotencyMechanicsValidation(
                CharacterTestGenesisPotencyMechanicsProbeKind.None,
                default,
                "harness-only",
                CharacterTestGenesisPotencyMechanicsSample.Empty,
                CharacterTestGenesisPotencyMechanicsSample.Empty,
                CharacterTestGenesisPotencyMechanicsSample.Empty,
                CharacterTestGenesisPotencyMechanicsSample.Empty);

        public CharacterTestGenesisPotencyMechanicsValidation(
            CharacterTestGenesisPotencyMechanicsProbeKind scenario,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisPotencyMechanicsSample fireDryGrass,
            CharacterTestGenesisPotencyMechanicsSample fireStone,
            CharacterTestGenesisPotencyMechanicsSample hammerDryGrass,
            CharacterTestGenesisPotencyMechanicsSample hammerStone)
        {
            Scenario = scenario;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            FireDryGrass = fireDryGrass;
            FireStone = fireStone;
            HammerDryGrass = hammerDryGrass;
            HammerStone = hammerStone;
        }

        public CharacterTestGenesisPotencyMechanicsProbeKind Scenario { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisPotencyMechanicsSample FireDryGrass { get; }
        public CharacterTestGenesisPotencyMechanicsSample FireStone { get; }
        public CharacterTestGenesisPotencyMechanicsSample HammerDryGrass { get; }
        public CharacterTestGenesisPotencyMechanicsSample HammerStone { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} scenario={1} frame={2} fireDryGrass=[{3}] fireStone=[{4}] hammerDryGrass=[{5}] hammerStone=[{6}]",
                StateModel,
                Scenario,
                Frame.Value,
                FireDryGrass.ToSummaryLine(),
                FireStone.ToSummaryLine(),
                HammerDryGrass.ToSummaryLine(),
                HammerStone.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisPotencyMechanicsSample
    {
        public static readonly CharacterTestGenesisPotencyMechanicsSample Empty =
            new CharacterTestGenesisPotencyMechanicsSample(
                string.Empty,
                0,
                CharacterTestGenesisPotencyMaterialKind.None,
                string.Empty,
                CharacterTestGenesisPotencyInfluenceKind.None,
                string.Empty,
                CharacterTestGenesisPotencyInputKind.None,
                CharacterTestGenesisForceOriginKind.SelfWeight,
                default,
                "harness-only",
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyNextContactSummary.Empty,
                "none",
                burningThresholdMet: false,
                crushThresholdMet: false,
                crackThresholdMet: false,
                CharacterTestGenesisPotencyResultKind.None,
                "none",
                externalTriggeredDirectly: false,
                physicsBodyCount: 0,
                physicsColliderCount: 0,
                physicsQueryHitCount: 0,
                physicsExplainHitCount: 0,
                physicsCandidateCount: 0,
                physicsPostFilterCandidateCount: 0,
                contactSpeed: 0f,
                contactDisplacement: 0f,
                hasContact: false);

        public CharacterTestGenesisPotencyMechanicsSample(
            string sampleStableId,
            int sampleNumericId,
            CharacterTestGenesisPotencyMaterialKind materialKind,
            string materialStableId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            string influenceStableId,
            CharacterTestGenesisPotencyInputKind inputKind,
            CharacterTestGenesisForceOriginKind forceOrigin,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisPotencyTriplet before,
            CharacterTestGenesisPotencyTriplet rawDelta,
            CharacterTestGenesisPotencyTriplet appliedDelta,
            CharacterTestGenesisPotencyTriplet ignoredDelta,
            CharacterTestGenesisPotencyTriplet after,
            CharacterTestGenesisPotencyNextContactSummary nextContact,
            string immunitySummary,
            bool burningThresholdMet,
            bool crushThresholdMet,
            bool crackThresholdMet,
            CharacterTestGenesisPotencyResultKind result,
            string resultSource,
            bool externalTriggeredDirectly,
            int physicsBodyCount,
            int physicsColliderCount,
            int physicsQueryHitCount,
            int physicsExplainHitCount,
            int physicsCandidateCount,
            int physicsPostFilterCandidateCount,
            float contactSpeed,
            float contactDisplacement,
            bool hasContact)
        {
            SampleStableId = sampleStableId ?? string.Empty;
            SampleNumericId = sampleNumericId;
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            InfluenceKind = influenceKind;
            InfluenceStableId = influenceStableId ?? string.Empty;
            InputKind = inputKind;
            ForceOrigin = forceOrigin;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            Before = before;
            RawDelta = rawDelta;
            AppliedDelta = appliedDelta;
            IgnoredDelta = ignoredDelta;
            After = after;
            NextContact = nextContact;
            ImmunitySummary = string.IsNullOrWhiteSpace(immunitySummary) ? "none" : immunitySummary;
            BurningThresholdMet = burningThresholdMet;
            CrushThresholdMet = crushThresholdMet;
            CrackThresholdMet = crackThresholdMet;
            Result = result;
            ResultSource = string.IsNullOrWhiteSpace(resultSource) ? "threshold-not-met" : resultSource;
            ExternalTriggeredDirectly = externalTriggeredDirectly;
            PhysicsBodyCount = physicsBodyCount < 0 ? 0 : physicsBodyCount;
            PhysicsColliderCount = physicsColliderCount < 0 ? 0 : physicsColliderCount;
            PhysicsQueryHitCount = physicsQueryHitCount < 0 ? 0 : physicsQueryHitCount;
            PhysicsExplainHitCount = physicsExplainHitCount < 0 ? 0 : physicsExplainHitCount;
            PhysicsCandidateCount = physicsCandidateCount < 0 ? 0 : physicsCandidateCount;
            PhysicsPostFilterCandidateCount =
                physicsPostFilterCandidateCount < 0 ? 0 : physicsPostFilterCandidateCount;
            ContactSpeed = contactSpeed < 0f ? 0f : contactSpeed;
            ContactDisplacement = contactDisplacement < 0f ? 0f : contactDisplacement;
            HasContact = hasContact;
        }

        public string SampleStableId { get; }
        public int SampleNumericId { get; }
        public CharacterTestGenesisPotencyMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public CharacterTestGenesisPotencyInfluenceKind InfluenceKind { get; }
        public string InfluenceStableId { get; }
        public CharacterTestGenesisPotencyInputKind InputKind { get; }
        public CharacterTestGenesisForceOriginKind ForceOrigin { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisPotencyTriplet Before { get; }
        public CharacterTestGenesisPotencyTriplet RawDelta { get; }
        public CharacterTestGenesisPotencyTriplet AppliedDelta { get; }
        public CharacterTestGenesisPotencyTriplet IgnoredDelta { get; }
        public CharacterTestGenesisPotencyTriplet After { get; }
        public CharacterTestGenesisPotencyNextContactSummary NextContact { get; }
        public string ImmunitySummary { get; }
        public bool BurningThresholdMet { get; }
        public bool CrushThresholdMet { get; }
        public bool CrackThresholdMet { get; }
        public CharacterTestGenesisPotencyResultKind Result { get; }
        public string ResultSource { get; }
        public bool ExternalTriggeredDirectly { get; }
        public int PhysicsBodyCount { get; }
        public int PhysicsColliderCount { get; }
        public int PhysicsQueryHitCount { get; }
        public int PhysicsExplainHitCount { get; }
        public int PhysicsCandidateCount { get; }
        public int PhysicsPostFilterCandidateCount { get; }
        public float ContactSpeed { get; }
        public float ContactDisplacement { get; }
        public bool HasContact { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "sample={0} numericId={1} material={2}/{3} influence={4}/{5} input={6} forceOrigin={7} frame={8} stateModel={9} beforeDPS={10} rawDelta={11} appliedDelta={12} ignoredDelta={13} immunity={14} afterDPS={15} nextContact=[{16}] thresholds=burning:{17},crush:{18},crack:{19} result={20} resultSource={21} directStateWrite={22} contact={23} contactSpeed={24:0.###} contactDisplacement={25:0.###} aabbBodies={26} aabbColliders={27} aabbQueryHits={28} aabbExplainHits={29} aabbCandidates={30} aabbPostFilterCandidates={31}",
                SampleStableId,
                SampleNumericId,
                MaterialKind,
                MaterialStableId,
                InfluenceKind,
                InfluenceStableId,
                InputKind,
                ForceOrigin,
                Frame.Value,
                StateModel,
                Before.ToTupleString(),
                RawDelta.ToTupleString(),
                AppliedDelta.ToTupleString(),
                IgnoredDelta.ToTupleString(),
                ImmunitySummary,
                After.ToTupleString(),
                NextContact.ToSummaryLine(),
                BurningThresholdMet ? "true" : "false",
                CrushThresholdMet ? "true" : "false",
                CrackThresholdMet ? "true" : "false",
                Result,
                ResultSource,
                ExternalTriggeredDirectly ? "true" : "false",
                HasContact ? "true" : "false",
                ContactSpeed,
                ContactDisplacement,
                PhysicsBodyCount,
                PhysicsColliderCount,
                PhysicsQueryHitCount,
                PhysicsExplainHitCount,
                PhysicsCandidateCount,
                PhysicsPostFilterCandidateCount);
        }
    }

    internal readonly struct CharacterTestGenesisPotencyNextContactSummary
    {
        public static readonly CharacterTestGenesisPotencyNextContactSummary Empty =
            new CharacterTestGenesisPotencyNextContactSummary(
                burningResultActive: false,
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyTriplet.Zero,
                supportBefore: 0,
                supportAfter: 0,
                structureBefore: 0,
                structureAfter: 0,
                blockResistanceBefore: 0f,
                blockResistanceAfter: 0f,
                contactDisplacementBefore: 0f,
                contactDisplacementAfter: 0f);

        public CharacterTestGenesisPotencyNextContactSummary(
            bool burningResultActive,
            CharacterTestGenesisPotencyTriplet resultDelta,
            CharacterTestGenesisPotencyTriplet resultDps,
            int supportBefore,
            int supportAfter,
            int structureBefore,
            int structureAfter,
            float blockResistanceBefore,
            float blockResistanceAfter,
            float contactDisplacementBefore,
            float contactDisplacementAfter)
        {
            BurningResultActive = burningResultActive;
            ResultDelta = resultDelta;
            ResultDps = resultDps;
            SupportBefore = supportBefore < 0 ? 0 : supportBefore;
            SupportAfter = supportAfter < 0 ? 0 : supportAfter;
            StructureBefore = structureBefore < 0 ? 0 : structureBefore;
            StructureAfter = structureAfter < 0 ? 0 : structureAfter;
            BlockResistanceBefore = blockResistanceBefore < 0f ? 0f : blockResistanceBefore;
            BlockResistanceAfter = blockResistanceAfter < 0f ? 0f : blockResistanceAfter;
            ContactDisplacementBefore = contactDisplacementBefore < 0f ? 0f : contactDisplacementBefore;
            ContactDisplacementAfter = contactDisplacementAfter < 0f ? 0f : contactDisplacementAfter;
        }

        public bool BurningResultActive { get; }
        public CharacterTestGenesisPotencyTriplet ResultDelta { get; }
        public CharacterTestGenesisPotencyTriplet ResultDps { get; }
        public int SupportBefore { get; }
        public int SupportAfter { get; }
        public int StructureBefore { get; }
        public int StructureAfter { get; }
        public float BlockResistanceBefore { get; }
        public float BlockResistanceAfter { get; }
        public float ContactDisplacementBefore { get; }
        public float ContactDisplacementAfter { get; }
        public bool HasMeasurableDifference =>
            BurningResultActive
            && (ResultDelta.P < 0
                || SupportAfter < SupportBefore
                || StructureAfter < StructureBefore
                || BlockResistanceAfter < BlockResistanceBefore
                || ContactDisplacementAfter > ContactDisplacementBefore);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "burningResult={0} resultDelta={1} resultDPS={2} support={3}->{4} structure={5}->{6} blockResistance={7:0.###}->{8:0.###} contactDisplacement={9:0.###}->{10:0.###} weakened={11}",
                BurningResultActive ? "true" : "false",
                ResultDelta.ToTupleString(),
                ResultDps.ToTupleString(),
                SupportBefore,
                SupportAfter,
                StructureBefore,
                StructureAfter,
                BlockResistanceBefore,
                BlockResistanceAfter,
                ContactDisplacementBefore,
                ContactDisplacementAfter,
                HasMeasurableDifference ? "true" : "false");
        }
    }

    public readonly struct CharacterTestGenesisPotencyTriplet : IEquatable<CharacterTestGenesisPotencyTriplet>
    {
        public static readonly CharacterTestGenesisPotencyTriplet Zero =
            new CharacterTestGenesisPotencyTriplet(0, 0, 0);

        public CharacterTestGenesisPotencyTriplet(int motion, int cohesion, int scatter)
        {
            Motion = motion;
            Cohesion = cohesion;
            Scatter = scatter;
        }

        public int Motion { get; }
        public int Cohesion { get; }
        public int Scatter { get; }
        public int D => Motion;
        public int P => Cohesion;
        public int S => Scatter;

        public static CharacterTestGenesisPotencyTriplet operator +(
            CharacterTestGenesisPotencyTriplet left,
            CharacterTestGenesisPotencyTriplet right)
        {
            return new CharacterTestGenesisPotencyTriplet(
                left.Motion + right.Motion,
                left.Cohesion + right.Cohesion,
                left.Scatter + right.Scatter);
        }

        public static CharacterTestGenesisPotencyTriplet operator -(
            CharacterTestGenesisPotencyTriplet left,
            CharacterTestGenesisPotencyTriplet right)
        {
            return new CharacterTestGenesisPotencyTriplet(
                left.Motion - right.Motion,
                left.Cohesion - right.Cohesion,
                left.Scatter - right.Scatter);
        }

        public bool Equals(CharacterTestGenesisPotencyTriplet other)
        {
            return Motion == other.Motion && Cohesion == other.Cohesion && Scatter == other.Scatter;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestGenesisPotencyTriplet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Motion;
                hash = (hash * 397) ^ Cohesion;
                hash = (hash * 397) ^ Scatter;
                return hash;
            }
        }

        public string ToTupleString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0},{1},{2})", Motion, Cohesion, Scatter);
        }
    }

    internal enum CharacterTestGenesisPotencyMaterialKind
    {
        None = 0,
        DryGrass = 1,
        Stone = 2
    }

    public enum CharacterTestGenesisPotencyInfluenceKind
    {
        None = 0,
        Fire = 1,
        Hammer = 2,
        Condense = 3
    }

    internal enum CharacterTestGenesisPotencyInputKind
    {
        None = 0,
        Continuous = 1,
        Impact = 2
    }

    public enum CharacterTestGenesisPotencyResultKind
    {
        None = 0,
        Burning = 1,
        CrushedOrBroken = 2,
        Cracked = 3,
        CondensedLayer = 4
    }
}
