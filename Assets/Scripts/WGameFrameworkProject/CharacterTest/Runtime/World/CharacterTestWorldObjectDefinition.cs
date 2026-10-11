using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestWorldObjectKind
    {
        Unknown = 0,
        MovableNaturalObject = 1,
        StaticBoundary = 2,
        LevelPoint = 3,
        LevelRegion = 4
    }

    public static class CharacterTestWorldObjectDefinitionIds
    {
        public const int MovableSlickLight = 101;
        public const int MovableRoughHeavy = 102;
        public const int MovableElasticSeed = 103;
        public const int MovableContactA = 104;
        public const int MovableContactB = 105;
        public const int MovableSphere = 110;
        public const int MovableCapsule = 120;
        public const int StaticDefaultBoundary = 1001;
        public const int StaticRoughBoundary = 1002;
        public const int StaticHardSlickSurface = 1011;
        public const int StaticRoughSurface = 1012;
        public const int StaticElasticSurface = 1013;
        public const int StaticBouncePadSurface = 1014;
        public const int StaticSphereProbe = 1021;
        public const int StaticCapsuleProbe = 1022;
        public const int PointPlayerSpawn = 2001;
        public const int PointAiSpawn = 2002;
        public const int PointAiSpawnNearby = 2003;
        internal const int PointValidationCheckpoint = 2010;
        internal const int PointValidationMarker = 2011;
        public const int RegionStartArea = 3001;
        public const int RegionTestCourse = 3002;
    }

    public readonly struct CharacterTestWorldObjectMaterialFacet : IEquatable<CharacterTestWorldObjectMaterialFacet>
    {
        public CharacterTestWorldObjectMaterialFacet(CombatSurfaceMaterial material)
        {
            Material = NormalizeMaterial(material);
        }

        public CombatSurfaceMaterial Material { get; }

        public CombatSurfaceMaterialKind Kind => Material.Kind;

        public Fix64 Friction => Material.Friction;

        public Fix64 Elasticity => Material.Elasticity;

        public Fix64 FixedBounceHeight => Material.FixedBounceHeight;

        public CharacterTestWorldObjectMaterialFacet Normalize()
        {
            return new CharacterTestWorldObjectMaterialFacet(Material);
        }

        public bool Equals(CharacterTestWorldObjectMaterialFacet other)
        {
            return Material.Equals(other.Material);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectMaterialFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Material.GetHashCode();
        }

        private static CombatSurfaceMaterial NormalizeMaterial(CombatSurfaceMaterial material)
        {
            switch (material.Kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                case CombatSurfaceMaterialKind.Rough:
                case CombatSurfaceMaterialKind.Elastic:
                case CombatSurfaceMaterialKind.BouncePad:
                case CombatSurfaceMaterialKind.Default:
                    return material;
                default:
                    return CombatSurfaceMaterial.Default;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectShapeFacet : IEquatable<CharacterTestWorldObjectShapeFacet>
    {
        public CharacterTestWorldObjectShapeFacet(
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            int halfExtentXMillimeters,
            int halfExtentYMillimeters,
            int halfExtentZMillimeters)
        {
            ShapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(shapeKind);
            HalfExtentXMillimeters = Math.Max(1, halfExtentXMillimeters);
            HalfExtentYMillimeters = Math.Max(1, halfExtentYMillimeters);
            HalfExtentZMillimeters = Math.Max(1, halfExtentZMillimeters);
        }

        public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; }

        public int HalfExtentXMillimeters { get; }

        public int HalfExtentYMillimeters { get; }

        public int HalfExtentZMillimeters { get; }

        public static CharacterTestWorldObjectShapeFacet DefaultAabb =>
            new CharacterTestWorldObjectShapeFacet(
                CharacterTestMovableNaturalObjectShapeKind.Aabb,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters);

        public CharacterTestWorldObjectShapeFacet Normalize()
        {
            return new CharacterTestWorldObjectShapeFacet(
                ShapeKind,
                HalfExtentXMillimeters,
                HalfExtentYMillimeters,
                HalfExtentZMillimeters);
        }

        public bool Equals(CharacterTestWorldObjectShapeFacet other)
        {
            return ShapeKind == other.ShapeKind
                && HalfExtentXMillimeters == other.HalfExtentXMillimeters
                && HalfExtentYMillimeters == other.HalfExtentYMillimeters
                && HalfExtentZMillimeters == other.HalfExtentZMillimeters;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectShapeFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ShapeKind;
                hash = (hash * 397) ^ HalfExtentXMillimeters;
                hash = (hash * 397) ^ HalfExtentYMillimeters;
                hash = (hash * 397) ^ HalfExtentZMillimeters;
                return hash;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectPotencyFacet : IEquatable<CharacterTestWorldObjectPotencyFacet>
    {
        public CharacterTestWorldObjectPotencyFacet(
            CharacterTestGenesisPotencyTriplet basePotency,
            CharacterTestGenesisPotencyProfile profile)
        {
            BasePotency = CharacterTestGenesisPotencyRuntime.ClampState(basePotency);
            Profile = profile.Normalize();
        }

        public CharacterTestGenesisPotencyTriplet BasePotency { get; }

        public CharacterTestGenesisPotencyProfile Profile { get; }

        public static CharacterTestWorldObjectPotencyFacet Disabled =>
            new CharacterTestWorldObjectPotencyFacet(
                CharacterTestGenesisPotencyTriplet.Zero,
                CharacterTestGenesisPotencyProfile.Disabled);

        public CharacterTestWorldObjectPotencyFacet Normalize()
        {
            return new CharacterTestWorldObjectPotencyFacet(BasePotency, Profile);
        }

        public bool Equals(CharacterTestWorldObjectPotencyFacet other)
        {
            return BasePotency.Equals(other.BasePotency)
                && Profile.Equals(other.Profile);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectPotencyFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (BasePotency.GetHashCode() * 397) ^ Profile.GetHashCode();
            }
        }
    }

    public readonly struct CharacterTestMovableNaturalObjectFacet : IEquatable<CharacterTestMovableNaturalObjectFacet>
    {
        public CharacterTestMovableNaturalObjectFacet(int massGrams)
        {
            IsEnabled = true;
            MassGrams = massGrams > 0
                ? massGrams
                : CharacterTestMovableNaturalObjectSpawnSpec.DefaultMassGrams;
        }

        private CharacterTestMovableNaturalObjectFacet(bool isEnabled, int massGrams)
        {
            IsEnabled = isEnabled;
            MassGrams = massGrams;
        }

        public bool IsEnabled { get; }

        public int MassGrams { get; }

        public static CharacterTestMovableNaturalObjectFacet Missing =>
            new CharacterTestMovableNaturalObjectFacet(false, 0);

        public CharacterTestMovableNaturalObjectFacet Normalize()
        {
            return IsEnabled
                ? new CharacterTestMovableNaturalObjectFacet(MassGrams)
                : Missing;
        }

        public bool Equals(CharacterTestMovableNaturalObjectFacet other)
        {
            return IsEnabled == other.IsEnabled && MassGrams == other.MassGrams;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestMovableNaturalObjectFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsEnabled ? 1 : 0) * 397) ^ MassGrams;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectStaticColliderFacet : IEquatable<CharacterTestWorldObjectStaticColliderFacet>
    {
        public CharacterTestWorldObjectStaticColliderFacet(int combatLayer, int colliderId)
        {
            IsEnabled = true;
            CombatLayer = Math.Max(0, Math.Min(31, combatLayer));
            ColliderId = colliderId > 0 ? colliderId : 1;
        }

        private CharacterTestWorldObjectStaticColliderFacet(bool isEnabled, int combatLayer, int colliderId)
        {
            IsEnabled = isEnabled;
            CombatLayer = combatLayer;
            ColliderId = colliderId;
        }

        public bool IsEnabled { get; }

        public int CombatLayer { get; }

        public int ColliderId { get; }

        public static CharacterTestWorldObjectStaticColliderFacet Missing =>
            new CharacterTestWorldObjectStaticColliderFacet(false, 0, 0);

        public CharacterTestWorldObjectStaticColliderFacet Normalize()
        {
            return IsEnabled
                ? new CharacterTestWorldObjectStaticColliderFacet(CombatLayer, ColliderId)
                : Missing;
        }

        public bool Equals(CharacterTestWorldObjectStaticColliderFacet other)
        {
            return IsEnabled == other.IsEnabled
                && CombatLayer == other.CombatLayer
                && ColliderId == other.ColliderId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectStaticColliderFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsEnabled ? 1 : 0;
                hash = (hash * 397) ^ CombatLayer;
                hash = (hash * 397) ^ ColliderId;
                return hash;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectPointFacet : IEquatable<CharacterTestWorldObjectPointFacet>
    {
        public CharacterTestWorldObjectPointFacet(
            LevelWorldObjectPointKind pointKind,
            int characterId,
            int teamId,
            CharacterTestCharacterControlOwner controlOwner)
        {
            IsEnabled = true;
            PointKind = pointKind == LevelWorldObjectPointKind.PlayerSpawn
                || pointKind == LevelWorldObjectPointKind.AiSpawn
                || pointKind == LevelWorldObjectPointKind.Checkpoint
                || pointKind == LevelWorldObjectPointKind.Marker
                    ? pointKind
                    : LevelWorldObjectPointKind.Unknown;
            bool isSpawn = PointKind == LevelWorldObjectPointKind.PlayerSpawn
                || PointKind == LevelWorldObjectPointKind.AiSpawn;
            CharacterId = isSpawn
                ? (characterId > 0
                    ? characterId
                    : CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId)
                : 0;
            TeamId = isSpawn ? (teamId > 0 ? teamId : 1) : 0;
            ControlOwner = isSpawn ? controlOwner : CharacterTestCharacterControlOwner.None;
        }

        private CharacterTestWorldObjectPointFacet(
            bool isEnabled,
            LevelWorldObjectPointKind pointKind,
            int characterId,
            int teamId,
            CharacterTestCharacterControlOwner controlOwner)
        {
            IsEnabled = isEnabled;
            PointKind = pointKind;
            CharacterId = characterId;
            TeamId = teamId;
            ControlOwner = controlOwner;
        }

        public bool IsEnabled { get; }

        public LevelWorldObjectPointKind PointKind { get; }

        public int CharacterId { get; }

        public int TeamId { get; }

        public CharacterTestCharacterControlOwner ControlOwner { get; }

        public static CharacterTestWorldObjectPointFacet Missing =>
            new CharacterTestWorldObjectPointFacet(
                false,
                LevelWorldObjectPointKind.Unknown,
                0,
                0,
                CharacterTestCharacterControlOwner.None);

        public CharacterTestWorldObjectPointFacet Normalize()
        {
            return IsEnabled
                ? new CharacterTestWorldObjectPointFacet(PointKind, CharacterId, TeamId, ControlOwner)
                : Missing;
        }

        public bool Equals(CharacterTestWorldObjectPointFacet other)
        {
            return IsEnabled == other.IsEnabled
                && PointKind == other.PointKind
                && CharacterId == other.CharacterId
                && TeamId == other.TeamId
                && ControlOwner == other.ControlOwner;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectPointFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsEnabled ? 1 : 0;
                hash = (hash * 397) ^ (int)PointKind;
                hash = (hash * 397) ^ CharacterId;
                hash = (hash * 397) ^ TeamId;
                hash = (hash * 397) ^ (int)ControlOwner;
                return hash;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectRegionFacet : IEquatable<CharacterTestWorldObjectRegionFacet>
    {
        public CharacterTestWorldObjectRegionFacet(LevelWorldObjectRegionKind regionKind)
        {
            IsEnabled = true;
            RegionKind = regionKind == LevelWorldObjectRegionKind.TestRegion
                || regionKind == LevelWorldObjectRegionKind.TriggerRegion
                    ? regionKind
                    : LevelWorldObjectRegionKind.Unknown;
        }

        private CharacterTestWorldObjectRegionFacet(bool isEnabled, LevelWorldObjectRegionKind regionKind)
        {
            IsEnabled = isEnabled;
            RegionKind = regionKind;
        }

        public bool IsEnabled { get; }

        public LevelWorldObjectRegionKind RegionKind { get; }

        public static CharacterTestWorldObjectRegionFacet Missing =>
            new CharacterTestWorldObjectRegionFacet(false, LevelWorldObjectRegionKind.Unknown);

        public CharacterTestWorldObjectRegionFacet Normalize()
        {
            return IsEnabled ? new CharacterTestWorldObjectRegionFacet(RegionKind) : Missing;
        }

        public bool Equals(CharacterTestWorldObjectRegionFacet other)
        {
            return IsEnabled == other.IsEnabled && RegionKind == other.RegionKind;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectRegionFacet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsEnabled ? 1 : 0) * 397) ^ (int)RegionKind;
            }
        }
    }

    public readonly struct CharacterTestWorldObjectDefinition : IEquatable<CharacterTestWorldObjectDefinition>
    {
        public CharacterTestWorldObjectDefinition(
            int definitionId,
            string stableId,
            string displayName,
            CharacterTestWorldObjectKind objectKind,
            string category,
            CharacterTestWorldObjectMaterialFacet material,
            CharacterTestWorldObjectShapeFacet shape,
            CharacterTestWorldObjectPotencyFacet potency,
            CharacterTestMovableNaturalObjectFacet movableNaturalObject)
            : this(
                definitionId,
                stableId,
                displayName,
                objectKind,
                category,
                material,
                shape,
                potency,
                movableNaturalObject,
                CharacterTestWorldObjectStaticColliderFacet.Missing,
                CharacterTestWorldObjectPointFacet.Missing,
                CharacterTestWorldObjectRegionFacet.Missing)
        {
        }

        public CharacterTestWorldObjectDefinition(
            int definitionId,
            string stableId,
            string displayName,
            CharacterTestWorldObjectKind objectKind,
            string category,
            CharacterTestWorldObjectMaterialFacet material,
            CharacterTestWorldObjectShapeFacet shape,
            CharacterTestWorldObjectPotencyFacet potency,
            CharacterTestMovableNaturalObjectFacet movableNaturalObject,
            CharacterTestWorldObjectStaticColliderFacet staticCollider)
            : this(
                definitionId,
                stableId,
                displayName,
                objectKind,
                category,
                material,
                shape,
                potency,
                movableNaturalObject,
                staticCollider,
                CharacterTestWorldObjectPointFacet.Missing,
                CharacterTestWorldObjectRegionFacet.Missing)
        {
        }

        public CharacterTestWorldObjectDefinition(
            int definitionId,
            string stableId,
            string displayName,
            CharacterTestWorldObjectKind objectKind,
            string category,
            CharacterTestWorldObjectMaterialFacet material,
            CharacterTestWorldObjectShapeFacet shape,
            CharacterTestWorldObjectPotencyFacet potency,
            CharacterTestMovableNaturalObjectFacet movableNaturalObject,
            CharacterTestWorldObjectStaticColliderFacet staticCollider,
            CharacterTestWorldObjectPointFacet point,
            CharacterTestWorldObjectRegionFacet region)
        {
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "World object definition id must be greater than 0.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("World object stable id cannot be empty.", nameof(stableId));

            DefinitionId = definitionId;
            StableId = stableId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? StableId
                : displayName.Trim();
            ObjectKind = objectKind == CharacterTestWorldObjectKind.MovableNaturalObject
                || objectKind == CharacterTestWorldObjectKind.StaticBoundary
                || objectKind == CharacterTestWorldObjectKind.LevelPoint
                || objectKind == CharacterTestWorldObjectKind.LevelRegion
                ? objectKind
                : CharacterTestWorldObjectKind.Unknown;
            Category = string.IsNullOrWhiteSpace(category)
                ? CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory
                : category.Trim();
            Material = material.Normalize();
            Shape = shape.Equals(default(CharacterTestWorldObjectShapeFacet))
                ? CharacterTestWorldObjectShapeFacet.DefaultAabb
                : shape.Normalize();
            Potency = potency.Normalize();
            MovableNaturalObject = movableNaturalObject.Normalize();
            StaticCollider = staticCollider.Normalize();
            Point = point.Normalize();
            Region = region.Normalize();
        }

        public int DefinitionId { get; }

        public string StableId { get; }

        public string DisplayName { get; }

        public CharacterTestWorldObjectKind ObjectKind { get; }

        public string Category { get; }

        public CharacterTestWorldObjectMaterialFacet Material { get; }

        public CharacterTestWorldObjectShapeFacet Shape { get; }

        public CharacterTestWorldObjectPotencyFacet Potency { get; }

        public CharacterTestMovableNaturalObjectFacet MovableNaturalObject { get; }

        public CharacterTestWorldObjectStaticColliderFacet StaticCollider { get; }

        public CharacterTestWorldObjectPointFacet Point { get; }

        public CharacterTestWorldObjectRegionFacet Region { get; }

        public bool CanSpawnMovableNaturalObject =>
            ObjectKind == CharacterTestWorldObjectKind.MovableNaturalObject
            && MovableNaturalObject.IsEnabled;

        public bool CanSpawnStaticCollider =>
            ObjectKind == CharacterTestWorldObjectKind.StaticBoundary
            && StaticCollider.IsEnabled;

        public bool CanSpawnPoint =>
            ObjectKind == CharacterTestWorldObjectKind.LevelPoint
            && Point.IsEnabled;

        public bool CanSpawnRegion =>
            ObjectKind == CharacterTestWorldObjectKind.LevelRegion
            && Region.IsEnabled;

        public bool TryCreateMovableNaturalObjectSpawnSpec(
            int numericInstanceId,
            string instanceStableId,
            int halfExtentXMillimeters,
            int halfExtentYMillimeters,
            int halfExtentZMillimeters,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            out CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            spec = default;
            if (!CanSpawnMovableNaturalObject || numericInstanceId <= 0)
                return false;

            string resolvedStableId = string.IsNullOrWhiteSpace(instanceStableId)
                ? StableId
                : instanceStableId.Trim();
            spec = new CharacterTestMovableNaturalObjectSpawnSpec(
                resolvedStableId,
                numericInstanceId,
                Category,
                MovableNaturalObject.MassGrams,
                Material.Material,
                halfExtentXMillimeters > 0 ? halfExtentXMillimeters : Shape.HalfExtentXMillimeters,
                halfExtentYMillimeters > 0 ? halfExtentYMillimeters : Shape.HalfExtentYMillimeters,
                halfExtentZMillimeters > 0 ? halfExtentZMillimeters : Shape.HalfExtentZMillimeters,
                xMillimeters,
                yMillimeters,
                zMillimeters,
                Shape.ShapeKind,
                Potency.BasePotency,
                Potency.Profile);
            return true;
        }

        public bool Equals(CharacterTestWorldObjectDefinition other)
        {
            return DefinitionId == other.DefinitionId
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && ObjectKind == other.ObjectKind
                && string.Equals(Category, other.Category, StringComparison.Ordinal)
                && Material.Equals(other.Material)
                && Shape.Equals(other.Shape)
                && Potency.Equals(other.Potency)
                && MovableNaturalObject.Equals(other.MovableNaturalObject)
                && StaticCollider.Equals(other.StaticCollider)
                && Point.Equals(other.Point)
                && Region.Equals(other.Region);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DefinitionId;
                hash = (hash * 397) ^ StableId.GetHashCode();
                hash = (hash * 397) ^ DisplayName.GetHashCode();
                hash = (hash * 397) ^ (int)ObjectKind;
                hash = (hash * 397) ^ Category.GetHashCode();
                hash = (hash * 397) ^ Material.GetHashCode();
                hash = (hash * 397) ^ Shape.GetHashCode();
                hash = (hash * 397) ^ Potency.GetHashCode();
                hash = (hash * 397) ^ MovableNaturalObject.GetHashCode();
                hash = (hash * 397) ^ StaticCollider.GetHashCode();
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Region.GetHashCode();
                return hash;
            }
        }
    }

    public interface ICharacterTestWorldObjectDefinitionProvider
    {
        bool TryGetDefinition(int definitionId, out CharacterTestWorldObjectDefinition definition);

        bool TryGetDefinitionByStableId(string stableId, out CharacterTestWorldObjectDefinition definition);
    }

    public sealed class CharacterTestWorldObjectDefinitionCatalog :
        ICharacterTestWorldObjectDefinitionProvider,
        ILevelAuthoringDefinitionProvider
    {
        private readonly CharacterTestWorldObjectDefinition[] _definitions;
        private readonly Dictionary<int, CharacterTestWorldObjectDefinition> _definitionsById;
        private readonly Dictionary<string, CharacterTestWorldObjectDefinition> _definitionsByStableId;

        public CharacterTestWorldObjectDefinitionCatalog(
            IEnumerable<CharacterTestWorldObjectDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            var sorted = new List<CharacterTestWorldObjectDefinition>();
            _definitionsById = new Dictionary<int, CharacterTestWorldObjectDefinition>();
            _definitionsByStableId = new Dictionary<string, CharacterTestWorldObjectDefinition>(StringComparer.Ordinal);
            foreach (CharacterTestWorldObjectDefinition definition in definitions)
            {
                if (_definitionsById.ContainsKey(definition.DefinitionId))
                    throw new ArgumentException(
                        "Duplicate world object definition id: "
                        + definition.DefinitionId.ToString(CultureInfo.InvariantCulture)
                        + ".",
                        nameof(definitions));
                if (_definitionsByStableId.ContainsKey(definition.StableId))
                    throw new ArgumentException(
                        "Duplicate world object stable id: " + definition.StableId + ".",
                        nameof(definitions));

                _definitionsById.Add(definition.DefinitionId, definition);
                _definitionsByStableId.Add(definition.StableId, definition);
                sorted.Add(definition);
            }

            sorted.Sort((left, right) =>
            {
                int id = left.DefinitionId.CompareTo(right.DefinitionId);
                return id != 0
                    ? id
                    : string.CompareOrdinal(left.StableId, right.StableId);
            });
            _definitions = sorted.ToArray();
        }

        public IReadOnlyList<CharacterTestWorldObjectDefinition> Definitions => _definitions;

        public bool TryGetDefinition(int definitionId, out CharacterTestWorldObjectDefinition definition)
        {
            return _definitionsById.TryGetValue(definitionId, out definition);
        }

        public bool TryGetDefinitionByStableId(string stableId, out CharacterTestWorldObjectDefinition definition)
        {
            definition = default;
            return !string.IsNullOrWhiteSpace(stableId)
                && _definitionsByStableId.TryGetValue(stableId.Trim(), out definition);
        }

        public bool TryGetDefinition(int definitionId, out LevelAuthoringDefinitionSummary definition)
        {
            definition = default;
            if (!TryGetDefinition(definitionId, out CharacterTestWorldObjectDefinition characterDefinition))
                return false;

            definition = new LevelAuthoringDefinitionSummary(
                characterDefinition.DefinitionId,
                characterDefinition.StableId,
                characterDefinition.DisplayName,
                ResolveUsage(characterDefinition));
            return true;
        }

        private static LevelAuthoringDefinitionUsage ResolveUsage(
            CharacterTestWorldObjectDefinition definition)
        {
            if (definition.CanSpawnMovableNaturalObject)
                return LevelAuthoringDefinitionUsage.Movable;
            if (definition.CanSpawnStaticCollider)
                return LevelAuthoringDefinitionUsage.StaticCollider;
            if (definition.CanSpawnPoint)
                return LevelAuthoringDefinitionUsage.Point;
            if (definition.CanSpawnRegion)
                return LevelAuthoringDefinitionUsage.Region;
            return LevelAuthoringDefinitionUsage.Unknown;
        }

        public static CharacterTestWorldObjectDefinitionCatalog CreateCharacterTestSceneCatalog()
        {
            CharacterTestGenesisConfigRuntimeAdapter.TryCreate(
                CharacterTestGenesisConfigDefaults.CreateDefaultDocument(),
                out CharacterTestGenesisConfigRuntimeAdapter genesisConfig,
                out _);
            return CreateCharacterTestSceneCatalog(genesisConfig);
        }

        internal static bool TryCreateCharacterTestSceneCatalog(
            CharacterTestGenesisConfigDocument genesisConfigDocument,
            out CharacterTestWorldObjectDefinitionCatalog catalog,
            out CharacterTestGenesisConfigValidationReport report)
        {
            catalog = null;
            if (!CharacterTestGenesisConfigRuntimeAdapter.TryCreate(
                    genesisConfigDocument,
                    out CharacterTestGenesisConfigRuntimeAdapter genesisConfig,
                    out report))
            {
                return false;
            }

            ValidateRequiredPotencyProfile(report, genesisConfig, CharacterTestGenesisConfigDefaults.GrassBurningCrushProfileId);
            ValidateRequiredPotencyProfile(report, genesisConfig, CharacterTestGenesisConfigDefaults.StoneCrackProfileId);
            ValidateRequiredPotencyProfile(report, genesisConfig, CharacterTestGenesisConfigDefaults.CondensedLayerProfileId);
            if (!report.Success)
                return false;

            catalog = CreateCharacterTestSceneCatalog(genesisConfig);
            return true;
        }

        private static CharacterTestWorldObjectDefinitionCatalog CreateCharacterTestSceneCatalog(
            CharacterTestGenesisConfigRuntimeAdapter genesisConfig)
        {
            return new CharacterTestWorldObjectDefinitionCatalog(new[]
            {
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableSlickLight,
                    "movable.natural.slick_light",
                    "Slick Light",
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    18,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.HardSlick,
                        Fix64.FromInt(1),
                        Fix64.Zero),
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.GrassBurningCrushProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy,
                    "movable.natural.rough_heavy",
                    "Rough Heavy",
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    90,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.Rough,
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.StoneCrackProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableElasticSeed,
                    "movable.natural.elastic_seed",
                    "Elastic Seed",
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    30,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Elastic,
                        Fix64.FromInt(8),
                        Fix64.One),
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.CondensedLayerProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableContactA,
                    "movable.natural.contact_a",
                    "Contact A",
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    35,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.HardSlick,
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.GrassBurningCrushProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableContactB,
                    "movable.natural.contact_b",
                    "Contact B",
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    35,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.HardSlick,
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.GrassBurningCrushProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableSphere,
                    "movable.natural.sphere",
                    "Sphere",
                    "natural",
                    25,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Elastic,
                        Fix64.FromInt(6),
                        Fix64.FromRatio(75, 100)),
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.CondensedLayerProfileId)),
                Movable(
                    CharacterTestWorldObjectDefinitionIds.MovableCapsule,
                    "movable.natural.capsule",
                    "Capsule",
                    "natural",
                    65,
                    CharacterTestMovableNaturalObjectShapeKind.Capsule,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Rough,
                        Fix64.FromInt(18),
                        Fix64.Zero),
                    CreateRequiredPotencyFacet(
                        genesisConfig,
                        CharacterTestGenesisConfigDefaults.StoneCrackProfileId)),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    "static.boundary.default",
                    "Default Boundary",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Default,
                        Fix64.FromInt(10),
                        Fix64.Zero)),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticRoughBoundary,
                    "static.boundary.rough",
                    "Rough Boundary",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.Rough),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface,
                    "static.surface.hard_slick",
                    "Hard Slick Surface",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.HardSlick),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticRoughSurface,
                    "static.surface.rough",
                    "Rough Surface",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.Rough),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticElasticSurface,
                    "static.surface.elastic",
                    "Elastic Surface",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.Elastic),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticBouncePadSurface,
                    "static.surface.bounce_pad",
                    "Bounce Pad Surface",
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    CombatSurfaceMaterial.BouncePad),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticSphereProbe,
                    "static.probe.sphere",
                    "Static Sphere Probe",
                    CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Elastic,
                        Fix64.FromInt(8),
                        Fix64.FromRatio(6, 10))),
                StaticCollider(
                    CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe,
                    "static.probe.capsule",
                    "Static Capsule Probe",
                    CharacterTestMovableNaturalObjectShapeKind.Capsule,
                    new CombatSurfaceMaterial(
                        CombatSurfaceMaterialKind.Rough,
                        Fix64.FromInt(20),
                        Fix64.Zero)),
                Point(
                    CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                    "point.spawn.player",
                    "Player Spawn",
                    LevelWorldObjectPointKind.PlayerSpawn,
                    teamId: 1,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                Point(
                    CharacterTestWorldObjectDefinitionIds.PointAiSpawn,
                    "point.spawn.ai",
                    "AI Spawn",
                    LevelWorldObjectPointKind.AiSpawn,
                    teamId: 2,
                    CharacterTestCharacterControlOwner.None),
                Point(
                    CharacterTestWorldObjectDefinitionIds.PointAiSpawnNearby,
                    "point.spawn.ai_nearby",
                    "Nearby AI Spawn",
                    LevelWorldObjectPointKind.AiSpawn,
                    teamId: 2,
                    CharacterTestCharacterControlOwner.None),
                Point(
                    CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint,
                    "point.validation.checkpoint",
                    "Validation Checkpoint",
                    LevelWorldObjectPointKind.Checkpoint,
                    teamId: 0,
                    CharacterTestCharacterControlOwner.None),
                Point(
                    CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                    "point.validation.marker",
                    "Validation Marker",
                    LevelWorldObjectPointKind.Marker,
                    teamId: 0,
                    CharacterTestCharacterControlOwner.None),
                Region(
                    CharacterTestWorldObjectDefinitionIds.RegionStartArea,
                    "region.start",
                    "Start Area",
                    LevelWorldObjectRegionKind.TestRegion),
                Region(
                    CharacterTestWorldObjectDefinitionIds.RegionTestCourse,
                    "region.test_course",
                    "Test Course",
                    LevelWorldObjectRegionKind.TestRegion)
            });
        }

        private static CharacterTestWorldObjectDefinition Movable(
            int definitionId,
            string stableId,
            string displayName,
            string category,
            int massKg,
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            CombatSurfaceMaterial material,
            CharacterTestWorldObjectPotencyFacet potency)
        {
            return new CharacterTestWorldObjectDefinition(
                definitionId,
                stableId,
                displayName,
                CharacterTestWorldObjectKind.MovableNaturalObject,
                category,
                new CharacterTestWorldObjectMaterialFacet(material),
                new CharacterTestWorldObjectShapeFacet(
                    shapeKind,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters),
                potency,
                new CharacterTestMovableNaturalObjectFacet(
                    CharacterTestDeterministicUnits.KilogramsToGrams(massKg)));
        }

        private static CharacterTestWorldObjectDefinition StaticCollider(
            int definitionId,
            string stableId,
            string displayName,
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            CombatSurfaceMaterial material)
        {
            return new CharacterTestWorldObjectDefinition(
                definitionId,
                stableId,
                displayName,
                CharacterTestWorldObjectKind.StaticBoundary,
                "static",
                new CharacterTestWorldObjectMaterialFacet(material),
                new CharacterTestWorldObjectShapeFacet(
                    shapeKind,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters),
                CharacterTestWorldObjectPotencyFacet.Disabled,
                CharacterTestMovableNaturalObjectFacet.Missing,
                new CharacterTestWorldObjectStaticColliderFacet(combatLayer: 1, colliderId: 1));
        }

        private static CharacterTestWorldObjectDefinition Point(
            int definitionId,
            string stableId,
            string displayName,
            LevelWorldObjectPointKind pointKind,
            int teamId,
            CharacterTestCharacterControlOwner controlOwner)
        {
            bool isSpawn = pointKind == LevelWorldObjectPointKind.PlayerSpawn
                || pointKind == LevelWorldObjectPointKind.AiSpawn;
            return new CharacterTestWorldObjectDefinition(
                definitionId,
                stableId,
                displayName,
                CharacterTestWorldObjectKind.LevelPoint,
                "point",
                new CharacterTestWorldObjectMaterialFacet(CombatSurfaceMaterial.Default),
                CharacterTestWorldObjectShapeFacet.DefaultAabb,
                CharacterTestWorldObjectPotencyFacet.Disabled,
                CharacterTestMovableNaturalObjectFacet.Missing,
                CharacterTestWorldObjectStaticColliderFacet.Missing,
                new CharacterTestWorldObjectPointFacet(
                    pointKind,
                    isSpawn ? CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId : 0,
                    isSpawn ? teamId : 0,
                    isSpawn ? controlOwner : CharacterTestCharacterControlOwner.None),
                CharacterTestWorldObjectRegionFacet.Missing);
        }

        private static CharacterTestWorldObjectDefinition Region(
            int definitionId,
            string stableId,
            string displayName,
            LevelWorldObjectRegionKind regionKind)
        {
            return new CharacterTestWorldObjectDefinition(
                definitionId,
                stableId,
                displayName,
                CharacterTestWorldObjectKind.LevelRegion,
                "region",
                new CharacterTestWorldObjectMaterialFacet(CombatSurfaceMaterial.Default),
                CharacterTestWorldObjectShapeFacet.DefaultAabb,
                CharacterTestWorldObjectPotencyFacet.Disabled,
                CharacterTestMovableNaturalObjectFacet.Missing,
                CharacterTestWorldObjectStaticColliderFacet.Missing,
                CharacterTestWorldObjectPointFacet.Missing,
                new CharacterTestWorldObjectRegionFacet(regionKind));
        }

        private static CharacterTestWorldObjectPotencyFacet CreateRequiredPotencyFacet(
            CharacterTestGenesisConfigRuntimeAdapter genesisConfig,
            string profileId)
        {
            if (genesisConfig != null && genesisConfig.TryCreatePotencyFacet(profileId, out CharacterTestWorldObjectPotencyFacet potency))
                return potency;

            return CharacterTestWorldObjectPotencyFacet.Disabled;
        }

        private static void ValidateRequiredPotencyProfile(
            CharacterTestGenesisConfigValidationReport report,
            CharacterTestGenesisConfigRuntimeAdapter genesisConfig,
            string profileId)
        {
            if (genesisConfig != null && genesisConfig.TryCreatePotencyFacet(profileId, out _))
                return;

            report.AddError(
                "required-potency-profile-missing",
                "$.potencyProfiles",
                "CharacterTest scene catalog requires potency profile: " + profileId);
        }
    }
}
