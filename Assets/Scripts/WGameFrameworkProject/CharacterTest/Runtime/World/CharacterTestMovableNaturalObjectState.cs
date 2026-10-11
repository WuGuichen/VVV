using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestMovableObjectBehavior
    {
        Idle = 0,
        PushSliding = 1,
        Falling = 2,
        VerticalRebound = 3,
        ObjectContactReceived = 4,
        BoundaryBlocked = 5,
        BoundaryRebound = 6,
        ObjectContactSource = 7,
        ObjectRebound = 8,
        ObjectBlocked = 9,
        Sliding = 10,
        PressureBlocked = 11,
        ReboundReturnedForce = 12,
    }


    public enum CharacterTestMovableNaturalObjectShapeKind
    {
        Aabb = 0,
        Sphere = 1,
        Capsule = 2
    }

    public readonly struct CharacterTestMovableNaturalObjectSpawnSpec
    {
        public const string DefaultCategory = "movable-natural-object";
        public const int DefaultHalfExtentMillimeters = 250;
        public const int DefaultHeightMillimeters = 500;
        public const int DefaultMassGrams = 40000;

        public CharacterTestMovableNaturalObjectSpawnSpec(
            string stableId,
            int numericId,
            string category,
            int massGrams,
            CombatSurfaceMaterial material,
            int halfExtentXMillimeters,
            int halfExtentYMillimeters,
            int halfExtentZMillimeters,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestGenesisPotencyTriplet basePotency = default,
            CharacterTestGenesisPotencyProfile potencyProfile = default)
        {
            AuthoredStableLabel = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            StableLabelDigest = RuntimeTextContentDigest.Prepare(AuthoredStableLabel);
            NumericId = numericId > 0 ? numericId : 0;
            Category = string.IsNullOrWhiteSpace(category) ? DefaultCategory : category.Trim();
            CategoryDigest = RuntimeTextContentDigest.Prepare(Category);
            MassGrams = massGrams > 0 ? massGrams : DefaultMassGrams;
            Material = NormalizeMaterial(material);
            ShapeKind = NormalizeShapeKind(shapeKind);
            BasePotency = CharacterTestGenesisPotencyRuntime.ClampState(basePotency);
            PotencyProfile = potencyProfile.Normalize();
            HalfExtentXMillimeters = Math.Max(1, halfExtentXMillimeters);
            HalfExtentYMillimeters = Math.Max(1, halfExtentYMillimeters);
            HalfExtentZMillimeters = Math.Max(1, halfExtentZMillimeters);
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
        }

        public string StableId => DisplayLabel(AuthoredStableLabel, NumericId);
        public string AuthoredStableLabel { get; }
        public RuntimeHashDigest StableLabelDigest { get; }
        public RuntimeHashDigest CategoryDigest { get; }
        public int NumericId { get; }
        public string Category { get; }
        public int MassGrams { get; }
        public CombatSurfaceMaterial Material { get; }
        public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; }
        public CharacterTestGenesisPotencyTriplet BasePotency { get; }
        public CharacterTestGenesisPotencyProfile PotencyProfile { get; }
        public int HalfExtentXMillimeters { get; }
        public int HalfExtentYMillimeters { get; }
        public int HalfExtentZMillimeters { get; }
        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }

        public float MassKg => CharacterTestDeterministicUnits.GramsToKilograms(MassGrams);
        public float X => CharacterTestDeterministicUnits.MillimetersToMeters(XMillimeters);
        public float Y => CharacterTestDeterministicUnits.MillimetersToMeters(YMillimeters);
        public float Z => CharacterTestDeterministicUnits.MillimetersToMeters(ZMillimeters);

        public static CharacterTestMovableNaturalObjectSpawnSpec FromMeters(
            string stableId,
            int numericId,
            string category,
            float massKg,
            CombatSurfaceMaterial material,
            Vector3 halfExtents,
            Vector3 position,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestGenesisPotencyTriplet basePotency = default,
            CharacterTestGenesisPotencyProfile potencyProfile = default)
        {
            return new CharacterTestMovableNaturalObjectSpawnSpec(
                stableId,
                numericId,
                category,
                CharacterTestDeterministicUnits.KilogramsToGrams(massKg),
                material,
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.z),
                CharacterTestDeterministicUnits.MetersToMillimeters(position.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(position.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(position.z),
                shapeKind,
                basePotency,
                potencyProfile);
        }

        internal static string DisplayLabel(string label, int numericId) =>
            string.IsNullOrEmpty(label) ? "movable.natural." + numericId.ToString(CultureInfo.InvariantCulture) : label;

        private static CombatSurfaceMaterial NormalizeMaterial(CombatSurfaceMaterial material)
        {
            if (material.Kind == CombatSurfaceMaterialKind.Default
                || material.Kind == CombatSurfaceMaterialKind.HardSlick
                || material.Kind == CombatSurfaceMaterialKind.Rough
                || material.Kind == CombatSurfaceMaterialKind.Elastic
                || material.Kind == CombatSurfaceMaterialKind.BouncePad)
            {
                return material;
            }

            return CombatSurfaceMaterial.Default;
        }

        internal static CharacterTestMovableNaturalObjectShapeKind NormalizeShapeKind(
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return shapeKind;
                default:
                    return CharacterTestMovableNaturalObjectShapeKind.Aabb;
            }
        }
    }

    public enum CharacterTestMovableNaturalObjectContactPairKind
    {
        None = 0,
        Literal = 1,
        Ordered = 2,
        Interaction = 3,
        Boundary = 4,
        Character = 5,
        StaticCollider = 6,
    }

    public sealed class CharacterTestMovableNaturalObjectState
    {
        private const string InteractionContactPairPrefix = "interaction:";
        private const string BoundaryContactPairPrefix = "boundary:";

        private CharacterTestMovableNaturalObjectSpawnSpec _spawnSpec;
        private int _pendingVelocityXMillimetersPerSecond;
        private int _pendingVelocityZMillimetersPerSecond;
        private int _pendingPressure;
        private int _pendingContactCount;
        private CharacterTestMovableNaturalObjectContactPairKind _pendingContactPairKind;
        private int _pendingContactPairA;
        private int _pendingContactPairB;
        private CharacterTestMovableNaturalObjectContactPairKind _contactPairKind;
        private string _contactPair = string.Empty;
        private int _contactPairA;
        private int _contactPairB;
        private bool _contactPairDirty;
        private CharacterTestMovableNaturalObjectContactPairKind _cachedContactPairKind;
        private string _cachedContactPair = string.Empty;
        private int _cachedContactPairA;
        private int _cachedContactPairB;
        private CharacterTestGenesisPotencyTriplet _pendingPotency =
            CharacterTestGenesisPotencyTriplet.Zero;
        private CharacterTestGenesisPotencyInfluenceKind _pendingPotencyInfluence =
            CharacterTestGenesisPotencyInfluenceKind.None;
        private int _combatColliderSyncVersion;

        public CharacterTestMovableNaturalObjectState(
            CharacterTestMovableNaturalObjectSpawnSpec spawnSpec)
        {
            Reset(spawnSpec);
        }

        public string StableId => CharacterTestMovableNaturalObjectSpawnSpec.DisplayLabel(AuthoredStableLabel, NumericId);
        public string AuthoredStableLabel { get; private set; }
        public RuntimeHashDigest StableLabelDigest { get; private set; }
        public RuntimeHashDigest CategoryDigest { get; private set; }
        public int NumericId { get; private set; }
        public string Category { get; private set; }
        public int MassGrams { get; private set; }
        public CombatSurfaceMaterial Material { get; private set; }
        public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; private set; }
        public CharacterTestGenesisPotencyTriplet BasePotency { get; private set; }
        public CharacterTestGenesisPotencyTriplet CurrentPotency { get; private set; }
        public CharacterTestGenesisPotencyTriplet PendingPotency => _pendingPotency;
        public CharacterTestGenesisPotencyInfluenceKind PendingPotencyInfluence =>
            _pendingPotencyInfluence;
        public CharacterTestGenesisPotencyProfile PotencyProfile { get; private set; }
        public CharacterTestGenesisPotencyApplicationRecord LatestPotencyApplication { get; private set; } =
            CharacterTestGenesisPotencyApplicationRecord.None;
        public int HalfExtentXMillimeters { get; private set; }
        public int HalfExtentYMillimeters { get; private set; }
        public int HalfExtentZMillimeters { get; private set; }
        public int XMillimeters { get; private set; }
        public int YMillimeters { get; private set; }
        public int ZMillimeters { get; private set; }
        public int VelocityXMillimetersPerSecond { get; private set; }
        public int VelocityYMillimetersPerSecond { get; private set; }
        public int VelocityZMillimetersPerSecond { get; private set; }
        public int ContactVelocityXMillimetersPerSecond { get; private set; }
        public int ContactVelocityZMillimetersPerSecond { get; private set; }
        public int ContactDisplacementXMillimeters { get; private set; }
        public int ContactDisplacementZMillimeters { get; private set; }
        public int ContactPressure { get; private set; }
        public string ContactPair => GetContactPair();
        public CharacterTestMovableNaturalObjectContactPairKind ContactKind => _contactPairKind;
        public int ContactAId => _contactPairA;
        public int ContactBId => _contactPairB;
        public CharacterTestMovableObjectBehavior BehaviorStateCode { get; private set; }
        public string BehaviorState => BehaviorStateText(BehaviorStateCode);

        public static string BehaviorStateText(CharacterTestMovableObjectBehavior code)
        {
            switch (code)
            {
                case CharacterTestMovableObjectBehavior.Idle: return "idle";
                case CharacterTestMovableObjectBehavior.PushSliding: return "push-sliding";
                case CharacterTestMovableObjectBehavior.Falling: return "falling";
                case CharacterTestMovableObjectBehavior.VerticalRebound: return "vertical-rebound";
                case CharacterTestMovableObjectBehavior.ObjectContactReceived: return "object-contact-received";
                case CharacterTestMovableObjectBehavior.BoundaryBlocked: return "boundary-blocked";
                case CharacterTestMovableObjectBehavior.BoundaryRebound: return "boundary-rebound";
                case CharacterTestMovableObjectBehavior.ObjectContactSource: return "object-contact-source";
                case CharacterTestMovableObjectBehavior.ObjectRebound: return "object-rebound";
                case CharacterTestMovableObjectBehavior.ObjectBlocked: return "object-blocked";
                case CharacterTestMovableObjectBehavior.Sliding: return "sliding";
                case CharacterTestMovableObjectBehavior.PressureBlocked: return "pressure-blocked";
                case CharacterTestMovableObjectBehavior.ReboundReturnedForce: return "rebound-returned-force";
                default: throw new ArgumentOutOfRangeException(nameof(code));
            }
        }
        public int ContactCount { get; private set; }
        internal int CombatColliderSyncVersion => _combatColliderSyncVersion;

        public CharacterTestMovableNaturalObjectSpawnSpec SpawnSpec => _spawnSpec;
        public float MassKg => CharacterTestDeterministicUnits.GramsToKilograms(MassGrams);
        public float X => CharacterTestDeterministicUnits.MillimetersToMeters(XMillimeters);
        public float Y => CharacterTestDeterministicUnits.MillimetersToMeters(YMillimeters);
        public float Z => CharacterTestDeterministicUnits.MillimetersToMeters(ZMillimeters);
        public Vector3 Position => new Vector3(X, Y, Z);
        public Vector3 HalfExtents => new Vector3(
            CharacterTestDeterministicUnits.MillimetersToMeters(HalfExtentXMillimeters),
            CharacterTestDeterministicUnits.MillimetersToMeters(HalfExtentYMillimeters),
            CharacterTestDeterministicUnits.MillimetersToMeters(HalfExtentZMillimeters));
        public Vector2 HorizontalVelocity => new Vector2(
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(VelocityXMillimetersPerSecond),
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(VelocityZMillimetersPerSecond));
        public Vector2 ContactVelocity => new Vector2(
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(ContactVelocityXMillimetersPerSecond),
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(ContactVelocityZMillimetersPerSecond));
        public Vector2 ContactDisplacement => new Vector2(
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactDisplacementXMillimeters),
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactDisplacementZMillimeters));

        public void Reset()
        {
            Reset(_spawnSpec);
        }

        public void Reset(CharacterTestMovableNaturalObjectSpawnSpec spawnSpec)
        {
            _spawnSpec = spawnSpec;
            AuthoredStableLabel = spawnSpec.AuthoredStableLabel;
            StableLabelDigest = spawnSpec.StableLabelDigest;
            CategoryDigest = spawnSpec.CategoryDigest;
            NumericId = spawnSpec.NumericId;
            Category = spawnSpec.Category;
            MassGrams = spawnSpec.MassGrams;
            Material = spawnSpec.Material;
            ShapeKind = spawnSpec.ShapeKind;
            BasePotency = spawnSpec.BasePotency;
            CurrentPotency = BasePotency;
            PotencyProfile = spawnSpec.PotencyProfile.Normalize();
            LatestPotencyApplication = CharacterTestGenesisPotencyApplicationRecord.None;
            HalfExtentXMillimeters = spawnSpec.HalfExtentXMillimeters;
            HalfExtentYMillimeters = spawnSpec.HalfExtentYMillimeters;
            HalfExtentZMillimeters = spawnSpec.HalfExtentZMillimeters;
            XMillimeters = spawnSpec.XMillimeters;
            YMillimeters = spawnSpec.YMillimeters;
            ZMillimeters = spawnSpec.ZMillimeters;
            VelocityXMillimetersPerSecond = 0;
            VelocityYMillimetersPerSecond = 0;
            VelocityZMillimetersPerSecond = 0;
            MarkCombatColliderDirty();
            ClearPendingVelocity();
            BeginFrame();
            BehaviorStateCode = CharacterTestMovableObjectBehavior.Idle;
        }

        public void BeginFrame()
        {
            ContactVelocityXMillimetersPerSecond = 0;
            ContactVelocityZMillimetersPerSecond = 0;
            ContactDisplacementXMillimeters = 0;
            ContactDisplacementZMillimeters = 0;
            ContactPressure = 0;
            ClearContactPair();
            ContactCount = 0;
            _pendingPotency = CharacterTestGenesisPotencyTriplet.Zero;
            _pendingPotencyInfluence = CharacterTestGenesisPotencyInfluenceKind.None;
        }

        public void AccumulateContactPush(
            int sourceId,
            int sourceMassGrams,
            int sourcePushScaleMilli,
            int sourceVelocityXMillimetersPerSecond,
            int sourceVelocityZMillimetersPerSecond)
        {
            if (sourceVelocityXMillimetersPerSecond == 0 && sourceVelocityZMillimetersPerSecond == 0)
                return;

            sourcePushScaleMilli = CharacterTestDeterministicMath.ClampScaleMilli(sourcePushScaleMilli);
            if (sourcePushScaleMilli <= 0)
                return;

            int sourceSpeed = CharacterTestDeterministicMath.Magnitude(
                sourceVelocityXMillimetersPerSecond,
                sourceVelocityZMillimetersPerSecond);
            if (sourceSpeed <= 0)
                return;

            int safeSourceMass = Math.Max(CharacterTestCharacterPhysicsComponent.MinimumMassGrams, sourceMassGrams);
            int safeTargetMass = Math.Max(CharacterTestCharacterPhysicsComponent.MinimumMassGrams, MassGrams);
            int massTransferMilli = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)safeSourceMass * CharacterTestDeterministicUnits.ScaleFactorScale,
                safeSourceMass + safeTargetMass);
            int materialPushMilli = ResolveMaterialPushScaleMilli(Material);
            int transferMilli = CharacterTestDeterministicMath.MulDivRoundToInt(
                (long)massTransferMilli * sourcePushScaleMilli,
                materialPushMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            transferMilli = CharacterTestDeterministicMath.Clamp(transferMilli, 0, CharacterTestDeterministicUnits.ScaleFactorScale);
            if (transferMilli <= 0)
                return;

            int velocityX = CharacterTestDeterministicMath.MulDivRoundToInt(
                sourceVelocityXMillimetersPerSecond,
                transferMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            int velocityZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                sourceVelocityZMillimetersPerSecond,
                transferMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            AccumulatePendingVelocity(velocityX, velocityZ);
            _pendingPressure += CharacterTestDeterministicMath.DivideRoundToInt(
                (long)sourceSpeed * safeSourceMass,
                CharacterTestDeterministicUnits.PositionScale);
            SetPendingContactPair(
                CharacterTestMovableNaturalObjectContactPairKind.Character,
                sourceId,
                NumericId);
            _pendingContactCount++;
        }

        public void AccumulateObjectContact(
            CharacterTestMovableNaturalObjectState other,
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int pressure)
        {
            if (other == null)
                return;

            AccumulatePendingVelocity(velocityXMillimetersPerSecond, velocityZMillimetersPerSecond);
            _pendingPressure += Math.Max(0, pressure);
            SetPendingContactPair(
                CharacterTestMovableNaturalObjectContactPairKind.Ordered,
                NumericId,
                other.NumericId);
            _pendingContactCount++;
        }

        public void AccumulateInteractionFieldVelocity(
            int sourceId,
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int pressure)
        {
            if (velocityXMillimetersPerSecond == 0 && velocityZMillimetersPerSecond == 0)
                return;

            AccumulatePendingVelocity(velocityXMillimetersPerSecond, velocityZMillimetersPerSecond);
            _pendingPressure += Math.Max(0, pressure);
            SetPendingContactPair(
                CharacterTestMovableNaturalObjectContactPairKind.Interaction,
                sourceId,
                NumericId);
            _pendingContactCount++;
        }

        public void ApplySnapshot(CharacterTestMovableNaturalObjectSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            AuthoredStableLabel = snapshot.StableId ?? AuthoredStableLabel;
            StableLabelDigest = RuntimeTextContentDigest.Prepare(AuthoredStableLabel);
            NumericId = snapshot.NumericId;
            Category = string.IsNullOrWhiteSpace(snapshot.Category)
                ? CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory
                : snapshot.Category;
            MassGrams = snapshot.MassGrams > 0
                ? snapshot.MassGrams
                : CharacterTestMovableNaturalObjectSpawnSpec.DefaultMassGrams;
            Material = snapshot.CreateSurfaceMaterial();
            ShapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(
                (CharacterTestMovableNaturalObjectShapeKind)snapshot.ShapeKind);
            HalfExtentXMillimeters = Math.Max(1, snapshot.HalfExtentXMillimeters);
            HalfExtentYMillimeters = Math.Max(1, snapshot.HalfExtentYMillimeters);
            HalfExtentZMillimeters = Math.Max(1, snapshot.HalfExtentZMillimeters);
            XMillimeters = snapshot.XMillimeters;
            YMillimeters = snapshot.YMillimeters;
            ZMillimeters = snapshot.ZMillimeters;
            VelocityXMillimetersPerSecond = snapshot.VelocityXMillimetersPerSecond;
            VelocityYMillimetersPerSecond = snapshot.VelocityYMillimetersPerSecond;
            VelocityZMillimetersPerSecond = snapshot.VelocityZMillimetersPerSecond;
            ContactVelocityXMillimetersPerSecond = snapshot.ContactVelocityXMillimetersPerSecond;
            ContactVelocityZMillimetersPerSecond = snapshot.ContactVelocityZMillimetersPerSecond;
            ContactDisplacementXMillimeters = snapshot.ContactDisplacementXMillimeters;
            ContactDisplacementZMillimeters = snapshot.ContactDisplacementZMillimeters;
            ContactPressure = Math.Max(0, snapshot.ContactPressure);
            SetContactPair((CharacterTestMovableNaturalObjectContactPairKind)snapshot.ContactPairKind, snapshot.ContactAId, snapshot.ContactBId);
            CategoryDigest = RuntimeTextContentDigest.Prepare(Category);
            SetBehavior(snapshot.BehaviorState);
            ContactCount = Math.Max(0, snapshot.ContactCount);
            BasePotency = CharacterTestGenesisPotencyRuntime.ClampState(snapshot.CreateBasePotency());
            CurrentPotency = CharacterTestGenesisPotencyRuntime.ClampState(snapshot.CreateCurrentPotency());
            _pendingPotency = snapshot.CreatePendingPotency();
            _pendingPotencyInfluence = CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(
                (CharacterTestGenesisPotencyInfluenceKind)snapshot.PendingPotencyInfluenceKind);
            PotencyProfile = snapshot.CreatePotencyProfile().Normalize();
            LatestPotencyApplication = snapshot.CreateLatestPotencyApplicationRecord();
            _spawnSpec = new CharacterTestMovableNaturalObjectSpawnSpec(
                StableId,
                NumericId,
                Category,
                MassGrams,
                Material,
                HalfExtentXMillimeters,
                HalfExtentYMillimeters,
                HalfExtentZMillimeters,
                XMillimeters,
                YMillimeters,
                ZMillimeters,
                ShapeKind,
                BasePotency,
                PotencyProfile);
            MarkCombatColliderDirty();
        }

        public CharacterTestMovableNaturalObjectSnapshot CaptureSnapshot()
        {
            return CharacterTestMovableNaturalObjectSnapshot.FromState(this);
        }

        public CharacterTestGenesisPotencyApplicationRecord ApplyPotencyInfluence(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            RuntimeFrame frame)
        {
            CharacterTestGenesisPotencyInfluenceKind normalizedInfluence =
                CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(influenceKind);
            _pendingPotencyInfluence = normalizedInfluence;
            _pendingPotency = CharacterTestGenesisPotencyRuntime.CreateRawDelta(normalizedInfluence);
            LatestPotencyApplication = CharacterTestGenesisPotencyRuntime.Apply(
                CurrentPotency,
                PotencyProfile,
                normalizedInfluence,
                frame,
                NumericId,
                out CharacterTestGenesisPotencyTriplet after);
            CurrentPotency = after;
            _pendingPotencyInfluence = CharacterTestGenesisPotencyInfluenceKind.None;
            _pendingPotency = CharacterTestGenesisPotencyTriplet.Zero;
            return LatestPotencyApplication;
        }

        internal void ApplyPendingVelocity()
        {
            if (_pendingVelocityXMillimetersPerSecond == 0 && _pendingVelocityZMillimetersPerSecond == 0)
                return;

            VelocityXMillimetersPerSecond += _pendingVelocityXMillimetersPerSecond;
            VelocityZMillimetersPerSecond += _pendingVelocityZMillimetersPerSecond;
            ContactVelocityXMillimetersPerSecond += _pendingVelocityXMillimetersPerSecond;
            ContactVelocityZMillimetersPerSecond += _pendingVelocityZMillimetersPerSecond;
            ContactPressure += _pendingPressure;
            if (_pendingContactPairKind != CharacterTestMovableNaturalObjectContactPairKind.None)
            {
                SetContactPair(
                    _pendingContactPairKind,
                    _pendingContactPairA,
                    _pendingContactPairB);
            }

            ContactCount += _pendingContactCount;
            ClearPendingVelocity();
        }

        private void ClearPendingVelocity()
        {
            _pendingVelocityXMillimetersPerSecond = 0;
            _pendingVelocityZMillimetersPerSecond = 0;
            _pendingPressure = 0;
            _pendingContactCount = 0;
            _pendingContactPairKind = CharacterTestMovableNaturalObjectContactPairKind.None;
            _pendingContactPairA = 0;
            _pendingContactPairB = 0;
        }

        internal void MoveBy(int deltaXMillimeters, int deltaZMillimeters, int contactColliderId = 0)
        {
            if (deltaXMillimeters == 0 && deltaZMillimeters == 0)
                return;

            XMillimeters += deltaXMillimeters;
            ZMillimeters += deltaZMillimeters;
            MarkCombatColliderDirty();
            ContactDisplacementXMillimeters += deltaXMillimeters;
            ContactDisplacementZMillimeters += deltaZMillimeters;
            if (contactColliderId > 0)
                SetContactPair(CharacterTestMovableNaturalObjectContactPairKind.StaticCollider, contactColliderId, NumericId);
        }

        internal void MoveByContactPair(
            int deltaXMillimeters,
            int deltaZMillimeters,
            int a,
            int b)
        {
            if (deltaXMillimeters == 0 && deltaZMillimeters == 0)
                return;

            XMillimeters += deltaXMillimeters;
            ZMillimeters += deltaZMillimeters;
            MarkCombatColliderDirty();
            ContactDisplacementXMillimeters += deltaXMillimeters;
            ContactDisplacementZMillimeters += deltaZMillimeters;
            SetContactPair(
                CharacterTestMovableNaturalObjectContactPairKind.Ordered,
                a,
                b);
        }

        internal void SetHorizontalPosition(int xMillimeters, int zMillimeters)
        {
            if (XMillimeters == xMillimeters && ZMillimeters == zMillimeters)
                return;

            XMillimeters = xMillimeters;
            ZMillimeters = zMillimeters;
            MarkCombatColliderDirty();
        }

        internal void SetVerticalPosition(int yMillimeters)
        {
            if (YMillimeters == yMillimeters)
                return;

            YMillimeters = yMillimeters;
            MarkCombatColliderDirty();
        }

        private void MarkCombatColliderDirty()
        {
            unchecked
            {
                _combatColliderSyncVersion++;
            }
        }

        internal void SetVelocity(int velocityXMillimetersPerSecond, int velocityZMillimetersPerSecond)
        {
            VelocityXMillimetersPerSecond = velocityXMillimetersPerSecond;
            VelocityZMillimetersPerSecond = velocityZMillimetersPerSecond;
        }

        internal void SetVerticalVelocity(int velocityYMillimetersPerSecond)
        {
            VelocityYMillimetersPerSecond = velocityYMillimetersPerSecond;
        }

        internal void MarkStaticContact(
            int colliderId,
            int incomingVelocityX,
            int incomingVelocityZ,
            int displacementX,
            int displacementZ,
            int pressure,
            CharacterTestMovableObjectBehavior behaviorState)
        {
            MarkContact(
                CharacterTestMovableNaturalObjectContactPairKind.StaticCollider,
                colliderId,
                NumericId,
                incomingVelocityX,
                incomingVelocityZ,
                displacementX,
                displacementZ,
                pressure,
                behaviorState);
        }

        internal void MarkBoundaryContact(
            int boundaryMaterialId,
            int incomingVelocityX,
            int incomingVelocityZ,
            int displacementX,
            int displacementZ,
            int pressure,
            CharacterTestMovableObjectBehavior behaviorState)
        {
            MarkContact(
                CharacterTestMovableNaturalObjectContactPairKind.Boundary,
                NumericId,
                boundaryMaterialId,
                incomingVelocityX,
                incomingVelocityZ,
                displacementX,
                displacementZ,
                pressure,
                behaviorState);
        }

        internal void MarkObjectContact(
            CharacterTestMovableNaturalObjectState other,
            int incomingVelocityX,
            int incomingVelocityZ,
            int displacementX,
            int displacementZ,
            int pressure,
            CharacterTestMovableObjectBehavior behaviorState)
        {
            MarkContact(
                CharacterTestMovableNaturalObjectContactPairKind.Ordered,
                NumericId,
                other != null ? other.NumericId : 0,
                incomingVelocityX,
                incomingVelocityZ,
                displacementX,
                displacementZ,
                pressure,
                behaviorState);
        }

        private void MarkContact(
            CharacterTestMovableNaturalObjectContactPairKind contactPairKind,
            int contactPairA,
            int contactPairB,
            int incomingVelocityX,
            int incomingVelocityZ,
            int displacementX,
            int displacementZ,
            int pressure,
            CharacterTestMovableObjectBehavior behaviorState)
        {
            SetContactPair(contactPairKind, contactPairA, contactPairB);
            ContactVelocityXMillimetersPerSecond = incomingVelocityX;
            ContactVelocityZMillimetersPerSecond = incomingVelocityZ;
            ContactDisplacementXMillimeters += displacementX;
            ContactDisplacementZMillimeters += displacementZ;
            ContactPressure += Math.Max(0, pressure);
            ContactCount++;
            SetBehavior(behaviorState);
        }

        internal void SetBehavior(CharacterTestMovableObjectBehavior behaviorState)
        {
            if ((uint)behaviorState > (uint)CharacterTestMovableObjectBehavior.ReboundReturnedForce)
                throw new ArgumentOutOfRangeException(nameof(behaviorState));
            BehaviorStateCode = behaviorState;
        }

        public static bool IsValidContactPair(CharacterTestMovableNaturalObjectContactPairKind kind, int a, int b)
        {
            if (kind == CharacterTestMovableNaturalObjectContactPairKind.None) return a == 0 && b == 0;
            if (kind == CharacterTestMovableNaturalObjectContactPairKind.Boundary) return a > 0 && b >= 0 && b <= (int)CombatSurfaceMaterialKind.BouncePad;
            return (kind == CharacterTestMovableNaturalObjectContactPairKind.Ordered
                || kind == CharacterTestMovableNaturalObjectContactPairKind.Interaction
                || kind == CharacterTestMovableNaturalObjectContactPairKind.Character
                || kind == CharacterTestMovableNaturalObjectContactPairKind.StaticCollider) && a > 0 && b > 0;
        }

        public static string FormatContactPair(CharacterTestMovableNaturalObjectContactPairKind kind, int a, int b)
        {
            if (kind == CharacterTestMovableNaturalObjectContactPairKind.None) return string.Empty;
            return kind.ToString() + ":" + a.ToString(CultureInfo.InvariantCulture) + "->" + b.ToString(CultureInfo.InvariantCulture);
        }

        private string GetContactPair()
        {
            if (!_contactPairDirty) return _contactPair;
            _contactPair = FormatContactPair(_contactPairKind, _contactPairA, _contactPairB);
            _contactPairDirty = false;
            return _contactPair;
        }

        private void ClearContactPair()
        {
            _contactPairKind = CharacterTestMovableNaturalObjectContactPairKind.None;
            _contactPairA = _contactPairB = 0;
            _contactPair = string.Empty;
            _contactPairDirty = false;
        }

        private void SetContactPair(CharacterTestMovableNaturalObjectContactPairKind kind, int a, int b)
        {
            if (!IsValidContactPair(kind, a, b)) throw new ArgumentException("Invalid numerical contact relation.");
            if (kind == CharacterTestMovableNaturalObjectContactPairKind.Ordered && a > b) { int swap = a; a = b; b = swap; }
            if (_contactPairKind == kind && _contactPairA == a && _contactPairB == b) return;
            _contactPairKind = kind; _contactPairA = a; _contactPairB = b;
            _contactPairDirty = true;
        }

        private void SetPendingContactPair(CharacterTestMovableNaturalObjectContactPairKind kind, int a, int b)
        {
            _pendingContactPairKind = kind;
            _pendingContactPairA = a;
            _pendingContactPairB = b;
        }

        private void AccumulatePendingVelocity(int velocityX, int velocityZ)
        {
            _pendingVelocityXMillimetersPerSecond += velocityX;
            _pendingVelocityZMillimetersPerSecond += velocityZ;
        }

        internal static int ResolveMaterialPushScaleMilli(CombatSurfaceMaterial material)
        {
            switch (material.Kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                    return 1200;
                case CombatSurfaceMaterialKind.Rough:
                    return 600;
                default:
                    return 1000;
            }
        }
    }

    public sealed class CharacterTestMovableNaturalObjectSummary
    {
        public static readonly CharacterTestMovableNaturalObjectSummary Empty =
            new CharacterTestMovableNaturalObjectSummary(Array.Empty<CharacterTestMovableNaturalObjectSnapshot>());

        private readonly CharacterTestMovableNaturalObjectSnapshot[] _objects;

        public CharacterTestMovableNaturalObjectSummary(
            IReadOnlyList<CharacterTestMovableNaturalObjectSnapshot> objects)
        {
            if (objects == null || objects.Count == 0)
            {
                _objects = Array.Empty<CharacterTestMovableNaturalObjectSnapshot>();
                return;
            }

            _objects = new CharacterTestMovableNaturalObjectSnapshot[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                _objects[i] = objects[i]?.CloneForRollback();
        }

        public IReadOnlyList<CharacterTestMovableNaturalObjectSnapshot> Objects => _objects;
        public int Count => _objects.Length;

        public string ToSummaryLine()
        {
            if (_objects.Length == 0)
                return "stateModel=runtime-authority count=0";

            var lines = new List<string>(_objects.Length + 1)
            {
                "stateModel=runtime-authority count=" + _objects.Length.ToString(CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < _objects.Length; i++)
                lines.Add(_objects[i].ToSummaryLine(i));
            return string.Join("\n", lines);
        }
    }
}


