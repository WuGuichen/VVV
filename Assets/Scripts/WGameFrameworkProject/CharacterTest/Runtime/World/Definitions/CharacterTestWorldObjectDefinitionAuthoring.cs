using System;
using System.Collections.Generic;
using MxFramework.Resources;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestWorldObjectDefinitionFormats
    {
        public const string AuthoringV1 = "mx.characterTest.worldObjectDefinitions.authoring.v1";
        public const string RuntimeV1 = "mx.characterTest.worldObjectDefinitions.runtime.v1";
        public const string RuntimeCatalogKind = "mxframework.characterTest.worldObjectDefinitions";
        public const int SchemaVersion = 1;
    }

    public enum CharacterTestWorldObjectPresentationKind
    {
        Unknown = 0,
        PrefabResource = 1,
        PrimitiveAuthoring = 2
    }

    public enum CharacterTestWorldObjectPrimitiveKind
    {
        Unknown = 0,
        Cube = 1,
        Sphere = 2,
        Capsule = 3
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectDefinitionAuthoringDocument
    {
        public string Format { get; set; } = CharacterTestWorldObjectDefinitionFormats.AuthoringV1;
        public int SchemaVersion { get; set; } = CharacterTestWorldObjectDefinitionFormats.SchemaVersion;
        public List<CharacterTestWorldObjectDefinitionAuthoringEntry> Definitions { get; set; } =
            new List<CharacterTestWorldObjectDefinitionAuthoringEntry>();
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectDefinitionRuntimeDocument
    {
        public string Format { get; set; } = CharacterTestWorldObjectDefinitionFormats.RuntimeV1;
        public int SchemaVersion { get; set; } = CharacterTestWorldObjectDefinitionFormats.SchemaVersion;
        public string SourceHash { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public List<CharacterTestWorldObjectDefinitionAuthoringEntry> Definitions { get; set; } =
            new List<CharacterTestWorldObjectDefinitionAuthoringEntry>();
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectDefinitionAuthoringEntry
    {
        public bool Enabled { get; set; } = true;
        public bool Deprecated { get; set; }
        public int DefinitionId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ObjectKind { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public CharacterTestWorldObjectPresentationAuthoring Presentation { get; set; } =
            new CharacterTestWorldObjectPresentationAuthoring();
        public CharacterTestWorldObjectMaterialAuthoring Material { get; set; } =
            new CharacterTestWorldObjectMaterialAuthoring();
        public CharacterTestWorldObjectShapeAuthoring Shape { get; set; } =
            new CharacterTestWorldObjectShapeAuthoring();
        public CharacterTestWorldObjectPotencyAuthoring Potency { get; set; } =
            new CharacterTestWorldObjectPotencyAuthoring();
        public CharacterTestMovableNaturalObjectFacetAuthoring MovableNaturalObject { get; set; } =
            new CharacterTestMovableNaturalObjectFacetAuthoring();
        public CharacterTestStaticColliderFacetAuthoring StaticCollider { get; set; } =
            new CharacterTestStaticColliderFacetAuthoring();
        public CharacterTestPointFacetAuthoring Point { get; set; } =
            new CharacterTestPointFacetAuthoring();
        public CharacterTestRegionFacetAuthoring Region { get; set; } =
            new CharacterTestRegionFacetAuthoring();
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectPresentationAuthoring
    {
        public string Kind { get; set; } = CharacterTestWorldObjectPresentationKind.Unknown.ToString();
        public CharacterTestResourceKeyAuthoring ResourceKey { get; set; } = new CharacterTestResourceKeyAuthoring();
        public string PrimitiveKind { get; set; } = CharacterTestWorldObjectPrimitiveKind.Unknown.ToString();
    }

    [Serializable]
    public sealed class CharacterTestResourceKeyAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;

        public ResourceKey ToResourceKey()
        {
            return new ResourceKey(Id, TypeId, Variant, PackageId);
        }
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectMaterialAuthoring
    {
        public string Kind { get; set; } = string.Empty;
        public long FrictionRaw { get; set; }
        public long ElasticityRaw { get; set; }
        public long FixedBounceHeightRaw { get; set; }
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectShapeAuthoring
    {
        public string Kind { get; set; } = string.Empty;
        public int HalfExtentXMillimeters { get; set; }
        public int HalfExtentYMillimeters { get; set; }
        public int HalfExtentZMillimeters { get; set; }
    }

    [Serializable]
    public sealed class CharacterTestWorldObjectPotencyAuthoring
    {
        public int BaseMotion { get; set; }
        public int BaseCohesion { get; set; }
        public int BaseScatter { get; set; }
        public bool ImmuneMotion { get; set; }
        public bool ImmuneCohesion { get; set; }
        public bool ImmuneScatter { get; set; }
        public bool BurningEnabled { get; set; }
        public bool CrushEnabled { get; set; }
        public bool CrackEnabled { get; set; }
        public bool CondensedLayerEnabled { get; set; }
        public int BurningMinMotion { get; set; }
        public int BurningMinScatter { get; set; }
        public int BurningMaxCohesion { get; set; }
        public int CondensedLayerMinCohesion { get; set; }
        public int CondensedLayerMaxMotion { get; set; }
    }

    [Serializable]
    public sealed class CharacterTestMovableNaturalObjectFacetAuthoring
    {
        public bool Enabled { get; set; }
        public int MassGrams { get; set; }
    }

    [Serializable]
    public sealed class CharacterTestStaticColliderFacetAuthoring
    {
        public bool Enabled { get; set; }
        public int CombatLayer { get; set; }
        public int ColliderId { get; set; }
    }

    [Serializable]
    public sealed class CharacterTestPointFacetAuthoring
    {
        public bool Enabled { get; set; }
        public string PointKind { get; set; } = string.Empty;
        public int CharacterId { get; set; }
        public int TeamId { get; set; }
        public string ControlOwner { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class CharacterTestRegionFacetAuthoring
    {
        public bool Enabled { get; set; }
        public string RegionKind { get; set; } = string.Empty;
    }
}
