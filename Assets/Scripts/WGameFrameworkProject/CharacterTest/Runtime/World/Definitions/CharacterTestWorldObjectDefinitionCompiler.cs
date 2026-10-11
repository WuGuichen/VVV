using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Resources;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestWorldObjectPresentationBinding :
        IEquatable<CharacterTestWorldObjectPresentationBinding>
    {
        public CharacterTestWorldObjectPresentationBinding(
            int definitionId,
            CharacterTestWorldObjectPresentationKind kind,
            ResourceKey prefabResource,
            CharacterTestWorldObjectPrimitiveKind primitiveKind)
        {
            DefinitionId = definitionId;
            Kind = kind;
            PrefabResource = prefabResource;
            PrimitiveKind = primitiveKind;
        }

        public int DefinitionId { get; }
        public CharacterTestWorldObjectPresentationKind Kind { get; }
        public ResourceKey PrefabResource { get; }
        public CharacterTestWorldObjectPrimitiveKind PrimitiveKind { get; }

        public bool Equals(CharacterTestWorldObjectPresentationBinding other)
        {
            return DefinitionId == other.DefinitionId
                && Kind == other.Kind
                && PrefabResource.Equals(other.PrefabResource)
                && PrimitiveKind == other.PrimitiveKind;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestWorldObjectPresentationBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DefinitionId;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ PrefabResource.GetHashCode();
                hash = (hash * 397) ^ (int)PrimitiveKind;
                return hash;
            }
        }
    }

    public sealed class CharacterTestWorldObjectPresentationCatalog
    {
        private readonly CharacterTestWorldObjectPresentationBinding[] _bindings;
        private readonly Dictionary<int, CharacterTestWorldObjectPresentationBinding> _byDefinitionId;

        public CharacterTestWorldObjectPresentationCatalog(
            IEnumerable<CharacterTestWorldObjectPresentationBinding> bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            var sorted = new List<CharacterTestWorldObjectPresentationBinding>(bindings);
            sorted.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));
            _bindings = sorted.ToArray();
            _byDefinitionId = new Dictionary<int, CharacterTestWorldObjectPresentationBinding>(_bindings.Length);
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_byDefinitionId.ContainsKey(_bindings[i].DefinitionId))
                    throw new ArgumentException("Duplicate presentation definition id.", nameof(bindings));
                _byDefinitionId.Add(_bindings[i].DefinitionId, _bindings[i]);
            }
        }

        public IReadOnlyList<CharacterTestWorldObjectPresentationBinding> Bindings => _bindings;

        public bool TryGet(int definitionId, out CharacterTestWorldObjectPresentationBinding binding)
        {
            return _byDefinitionId.TryGetValue(definitionId, out binding);
        }
    }

    public sealed class CharacterTestWorldObjectDefinitionCompileResult
    {
        internal CharacterTestWorldObjectDefinitionCompileResult(
            CharacterTestWorldObjectDefinitionValidationReport validation,
            CharacterTestWorldObjectDefinitionCatalog catalog,
            CharacterTestWorldObjectPresentationCatalog presentations,
            string sourceHash,
            string contentHash,
            CharacterTestWorldObjectDefinitionRuntimeDocument runtimeDocument)
        {
            Validation = validation;
            Catalog = catalog;
            Presentations = presentations;
            SourceHash = sourceHash ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            RuntimeDocument = runtimeDocument;
        }

        public bool Success => Validation != null && Validation.Success && Catalog != null;
        public CharacterTestWorldObjectDefinitionValidationReport Validation { get; }
        public CharacterTestWorldObjectDefinitionCatalog Catalog { get; }
        public CharacterTestWorldObjectPresentationCatalog Presentations { get; }
        public string SourceHash { get; }
        public string ContentHash { get; }
        public CharacterTestWorldObjectDefinitionRuntimeDocument RuntimeDocument { get; }
    }

    public static class CharacterTestWorldObjectDefinitionCompiler
    {
        /// <remarks>AllocByDesign: startup/authoring compilation creates immutable runtime catalogs.</remarks>
        public static CharacterTestWorldObjectDefinitionCompileResult Compile(
            CharacterTestWorldObjectDefinitionAuthoringDocument document)
        {
            CharacterTestWorldObjectDefinitionValidationReport validation =
                CharacterTestWorldObjectDefinitionValidator.Validate(document);
            if (!validation.Success)
            {
                return new CharacterTestWorldObjectDefinitionCompileResult(
                    validation,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    null);
            }

            CharacterTestWorldObjectDefinitionAuthoringDocument normalized =
                CharacterTestWorldObjectDefinitionJson.NormalizeAuthoring(document);
            var definitions = new List<CharacterTestWorldObjectDefinition>();
            var presentations = new List<CharacterTestWorldObjectPresentationBinding>();
            var runtimeEntries = new List<CharacterTestWorldObjectDefinitionAuthoringEntry>();
            for (int i = 0; i < normalized.Definitions.Count; i++)
            {
                CharacterTestWorldObjectDefinitionAuthoringEntry entry = normalized.Definitions[i];
                if (!entry.Enabled || entry.Deprecated)
                    continue;

                definitions.Add(BuildDefinition(entry));
                presentations.Add(BuildPresentation(entry));
                runtimeEntries.Add(CharacterTestWorldObjectDefinitionJson.CloneRuntimeEntry(entry));
            }

            string sourceHash = CharacterTestWorldObjectDefinitionJson.ComputeAuthoringHash(normalized);
            var runtimeDocument = new CharacterTestWorldObjectDefinitionRuntimeDocument
            {
                SourceHash = sourceHash,
                Definitions = runtimeEntries
            };
            string contentHash = CharacterTestWorldObjectDefinitionJson.ComputeRuntimeContentHash(runtimeDocument);
            runtimeDocument.ContentHash = contentHash;
            return new CharacterTestWorldObjectDefinitionCompileResult(
                validation,
                new CharacterTestWorldObjectDefinitionCatalog(definitions),
                new CharacterTestWorldObjectPresentationCatalog(presentations),
                sourceHash,
                contentHash,
                runtimeDocument);
        }

        internal static CharacterTestWorldObjectDefinition BuildDefinition(
            CharacterTestWorldObjectDefinitionAuthoringEntry entry)
        {
            Enum.TryParse(entry.ObjectKind, false, out CharacterTestWorldObjectKind objectKind);
            Enum.TryParse(entry.Material.Kind, false, out CombatSurfaceMaterialKind materialKind);
            Enum.TryParse(entry.Shape.Kind, false, out CharacterTestMovableNaturalObjectShapeKind shapeKind);
            CharacterTestWorldObjectPotencyAuthoring potency = entry.Potency;
            var definitionPotency = new CharacterTestWorldObjectPotencyFacet(
                new CharacterTestGenesisPotencyTriplet(
                    potency.BaseMotion,
                    potency.BaseCohesion,
                    potency.BaseScatter),
                new CharacterTestGenesisPotencyProfile(
                    potency.ImmuneMotion,
                    potency.ImmuneCohesion,
                    potency.ImmuneScatter,
                    potency.BurningEnabled,
                    potency.CrushEnabled,
                    potency.CrackEnabled,
                    potency.CondensedLayerEnabled,
                    potency.BurningMinMotion,
                    potency.BurningMinScatter,
                    potency.BurningMaxCohesion,
                    potency.CondensedLayerMinCohesion,
                    potency.CondensedLayerMaxMotion));

            return new CharacterTestWorldObjectDefinition(
                entry.DefinitionId,
                entry.StableId,
                entry.DisplayName,
                objectKind,
                entry.Category,
                new CharacterTestWorldObjectMaterialFacet(
                    new CombatSurfaceMaterial(
                        materialKind,
                        Fix64.FromRaw(entry.Material.FrictionRaw),
                        Fix64.FromRaw(entry.Material.ElasticityRaw),
                        Fix64.FromRaw(entry.Material.FixedBounceHeightRaw))),
                new CharacterTestWorldObjectShapeFacet(
                    shapeKind,
                    entry.Shape.HalfExtentXMillimeters,
                    entry.Shape.HalfExtentYMillimeters,
                    entry.Shape.HalfExtentZMillimeters),
                definitionPotency,
                entry.MovableNaturalObject.Enabled
                    ? new CharacterTestMovableNaturalObjectFacet(entry.MovableNaturalObject.MassGrams)
                    : CharacterTestMovableNaturalObjectFacet.Missing,
                entry.StaticCollider.Enabled
                    ? new CharacterTestWorldObjectStaticColliderFacet(
                        entry.StaticCollider.CombatLayer,
                        entry.StaticCollider.ColliderId)
                    : CharacterTestWorldObjectStaticColliderFacet.Missing,
                BuildPoint(entry.Point),
                BuildRegion(entry.Region));
        }

        private static CharacterTestWorldObjectPointFacet BuildPoint(CharacterTestPointFacetAuthoring point)
        {
            if (!point.Enabled)
                return CharacterTestWorldObjectPointFacet.Missing;
            Enum.TryParse(point.PointKind, false, out LevelWorldObjectPointKind pointKind);
            Enum.TryParse(point.ControlOwner, false, out CharacterTestCharacterControlOwner controlOwner);
            return new CharacterTestWorldObjectPointFacet(
                pointKind,
                point.CharacterId,
                point.TeamId,
                controlOwner);
        }

        private static CharacterTestWorldObjectRegionFacet BuildRegion(CharacterTestRegionFacetAuthoring region)
        {
            if (!region.Enabled)
                return CharacterTestWorldObjectRegionFacet.Missing;
            Enum.TryParse(region.RegionKind, false, out LevelWorldObjectRegionKind regionKind);
            return new CharacterTestWorldObjectRegionFacet(regionKind);
        }

        internal static CharacterTestWorldObjectPresentationBinding BuildPresentation(
            CharacterTestWorldObjectDefinitionAuthoringEntry entry)
        {
            Enum.TryParse(
                entry.Presentation.Kind,
                false,
                out CharacterTestWorldObjectPresentationKind kind);
            Enum.TryParse(
                entry.Presentation.PrimitiveKind,
                false,
                out CharacterTestWorldObjectPrimitiveKind primitive);
            return new CharacterTestWorldObjectPresentationBinding(
                entry.DefinitionId,
                kind,
                entry.Presentation.ResourceKey?.ToResourceKey() ?? default,
                primitive);
        }
    }
}
