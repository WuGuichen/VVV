using System;
using MxFramework.Resources;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestWorldObjectDefinitionMigration
    {
        /// <remarks>AllocByDesign: one-time built-in catalog export for authoring migration.</remarks>
        public static CharacterTestWorldObjectDefinitionAuthoringDocument ExportBuiltInCatalog()
        {
            return Export(CharacterTestWorldObjectDefinitionCatalog.CreateCharacterTestSceneCatalog());
        }

        /// <remarks>AllocByDesign: one-time catalog export for authoring migration.</remarks>
        public static CharacterTestWorldObjectDefinitionAuthoringDocument Export(
            CharacterTestWorldObjectDefinitionCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var document = new CharacterTestWorldObjectDefinitionAuthoringDocument();
            for (int i = 0; i < catalog.Definitions.Count; i++)
                document.Definitions.Add(ExportDefinition(catalog.Definitions[i]));
            return document;
        }

        private static CharacterTestWorldObjectDefinitionAuthoringEntry ExportDefinition(
            CharacterTestWorldObjectDefinition definition)
        {
            CharacterTestGenesisPotencyProfile profile = definition.Potency.Profile;
            return new CharacterTestWorldObjectDefinitionAuthoringEntry
            {
                Enabled = true,
                Deprecated = false,
                DefinitionId = definition.DefinitionId,
                StableId = definition.StableId,
                DisplayName = definition.DisplayName,
                ObjectKind = definition.ObjectKind.ToString(),
                Category = definition.Category,
                Presentation = new CharacterTestWorldObjectPresentationAuthoring
                {
                    Kind = CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring.ToString(),
                    ResourceKey = new CharacterTestResourceKeyAuthoring
                    {
                        TypeId = ResourceTypeIds.GameObject
                    },
                    PrimitiveKind = ResolvePrimitive(definition.Shape.ShapeKind).ToString()
                },
                Material = new CharacterTestWorldObjectMaterialAuthoring
                {
                    Kind = definition.Material.Kind.ToString(),
                    FrictionRaw = definition.Material.Friction.RawValue,
                    ElasticityRaw = definition.Material.Elasticity.RawValue,
                    FixedBounceHeightRaw = definition.Material.FixedBounceHeight.RawValue
                },
                Shape = new CharacterTestWorldObjectShapeAuthoring
                {
                    Kind = definition.Shape.ShapeKind.ToString(),
                    HalfExtentXMillimeters = definition.Shape.HalfExtentXMillimeters,
                    HalfExtentYMillimeters = definition.Shape.HalfExtentYMillimeters,
                    HalfExtentZMillimeters = definition.Shape.HalfExtentZMillimeters
                },
                Potency = new CharacterTestWorldObjectPotencyAuthoring
                {
                    BaseMotion = definition.Potency.BasePotency.Motion,
                    BaseCohesion = definition.Potency.BasePotency.Cohesion,
                    BaseScatter = definition.Potency.BasePotency.Scatter,
                    ImmuneMotion = profile.ImmuneMotion,
                    ImmuneCohesion = profile.ImmuneCohesion,
                    ImmuneScatter = profile.ImmuneScatter,
                    BurningEnabled = profile.BurningEnabled,
                    CrushEnabled = profile.CrushEnabled,
                    CrackEnabled = profile.CrackEnabled,
                    CondensedLayerEnabled = profile.CondensedLayerEnabled,
                    BurningMinMotion = profile.BurningMinMotion,
                    BurningMinScatter = profile.BurningMinScatter,
                    BurningMaxCohesion = profile.BurningMaxCohesion,
                    CondensedLayerMinCohesion = profile.CondensedLayerMinCohesion,
                    CondensedLayerMaxMotion = profile.CondensedLayerMaxMotion
                },
                MovableNaturalObject = new CharacterTestMovableNaturalObjectFacetAuthoring
                {
                    Enabled = definition.MovableNaturalObject.IsEnabled,
                    MassGrams = definition.MovableNaturalObject.MassGrams
                },
                StaticCollider = new CharacterTestStaticColliderFacetAuthoring
                {
                    Enabled = definition.StaticCollider.IsEnabled,
                    CombatLayer = definition.StaticCollider.CombatLayer,
                    ColliderId = definition.StaticCollider.ColliderId
                },
                Point = new CharacterTestPointFacetAuthoring
                {
                    Enabled = definition.Point.IsEnabled,
                    PointKind = definition.Point.PointKind.ToString(),
                    CharacterId = definition.Point.CharacterId,
                    TeamId = definition.Point.TeamId,
                    ControlOwner = definition.Point.ControlOwner.ToString()
                },
                Region = new CharacterTestRegionFacetAuthoring
                {
                    Enabled = definition.Region.IsEnabled,
                    RegionKind = definition.Region.RegionKind.ToString()
                },
                Description = string.Empty
            };
        }

        private static CharacterTestWorldObjectPrimitiveKind ResolvePrimitive(
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    return CharacterTestWorldObjectPrimitiveKind.Sphere;
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return CharacterTestWorldObjectPrimitiveKind.Capsule;
                default:
                    return CharacterTestWorldObjectPrimitiveKind.Cube;
            }
        }
    }
}
