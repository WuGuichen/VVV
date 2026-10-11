using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestWorldObjectLevelAuthoringTests
    {
        private static readonly string[] ExpectedBuiltInDefinitionSnapshot =
        {
            "101|movable.natural.slick_light|Slick Light|MovableNaturalObject|movable-natural-object|"
                + "material=HardSlick,1000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,20,0;0,0,0,1,1,0,0,15,18,25,40,4|"
                + "movable=1,18000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "102|movable.natural.rough_heavy|Rough Heavy|MovableNaturalObject|movable-natural-object|"
                + "material=Rough,60000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,90,0;0,0,1,0,0,1,0,15,18,25,40,4|"
                + "movable=1,90000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "103|movable.natural.elastic_seed|Elastic Seed|MovableNaturalObject|movable-natural-object|"
                + "material=Elastic,8000000,1000000,0|shape=Aabb,250,250,250|"
                + "potency=4,18,2;0,0,0,0,0,0,1,15,18,25,40,4|"
                + "movable=1,30000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "104|movable.natural.contact_a|Contact A|MovableNaturalObject|movable-natural-object|"
                + "material=HardSlick,3000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,20,0;0,0,0,1,1,0,0,15,18,25,40,4|"
                + "movable=1,35000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "105|movable.natural.contact_b|Contact B|MovableNaturalObject|movable-natural-object|"
                + "material=HardSlick,3000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,20,0;0,0,0,1,1,0,0,15,18,25,40,4|"
                + "movable=1,35000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "110|movable.natural.sphere|Sphere|MovableNaturalObject|natural|"
                + "material=Elastic,6000000,750000,0|shape=Sphere,250,250,250|"
                + "potency=4,18,2;0,0,0,0,0,0,1,15,18,25,40,4|"
                + "movable=1,25000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "120|movable.natural.capsule|Capsule|MovableNaturalObject|natural|"
                + "material=Rough,18000000,0,0|shape=Capsule,250,250,250|"
                + "potency=0,90,0;0,0,1,0,0,1,0,15,18,25,40,4|"
                + "movable=1,65000|static=0,0,0|point=0,Unknown,0,0,None|region=0,Unknown",
            "1001|static.boundary.default|Default Boundary|StaticBoundary|static|"
                + "material=Default,10000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1002|static.boundary.rough|Rough Boundary|StaticBoundary|static|"
                + "material=Rough,60000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1011|static.surface.hard_slick|Hard Slick Surface|StaticBoundary|static|"
                + "material=HardSlick,3000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1012|static.surface.rough|Rough Surface|StaticBoundary|static|"
                + "material=Rough,60000000,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1013|static.surface.elastic|Elastic Surface|StaticBoundary|static|"
                + "material=Elastic,20000000,1000000,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1014|static.surface.bounce_pad|Bounce Pad Surface|StaticBoundary|static|"
                + "material=BouncePad,20000000,0,4000000|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1021|static.probe.sphere|Static Sphere Probe|StaticBoundary|static|"
                + "material=Elastic,8000000,600000,0|shape=Sphere,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "1022|static.probe.capsule|Static Capsule Probe|StaticBoundary|static|"
                + "material=Rough,20000000,0,0|shape=Capsule,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=1,1,1|point=0,Unknown,0,0,None|region=0,Unknown",
            "2001|point.spawn.player|Player Spawn|LevelPoint|point|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=1,PlayerSpawn,1001,1,LocalPlayer|region=0,Unknown",
            "2002|point.spawn.ai|AI Spawn|LevelPoint|point|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=1,AiSpawn,1001,2,None|region=0,Unknown",
            "2003|point.spawn.ai_nearby|Nearby AI Spawn|LevelPoint|point|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=1,AiSpawn,1001,2,None|region=0,Unknown",
            "2010|point.validation.checkpoint|Validation Checkpoint|LevelPoint|point|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=1,Checkpoint,0,0,None|region=0,Unknown",
            "2011|point.validation.marker|Validation Marker|LevelPoint|point|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=1,Marker,0,0,None|region=0,Unknown",
            "3001|region.start|Start Area|LevelRegion|region|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=0,Unknown,0,0,None|region=1,TestRegion",
            "3002|region.test_course|Test Course|LevelRegion|region|"
                + "material=Default,0,0,0|shape=Aabb,250,250,250|"
                + "potency=0,0,0;0,0,0,0,0,0,0,0,0,0,0,0|"
                + "movable=0,0|static=0,0,0|point=0,Unknown,0,0,None|region=1,TestRegion"
        };

        [Test]
        public void BuiltInWorldObjectCatalog_MatchesP20FieldSnapshotAndEqualityContract()
        {
            CharacterTestWorldObjectDefinitionCatalog catalog =
                CharacterTestWorldObjectDefinitionCatalog.CreateCharacterTestSceneCatalog();
            string[] actual = BuildDefinitionSnapshot(catalog.Definitions);

            CollectionAssert.AreEqual(ExpectedBuiltInDefinitionSnapshot, actual);
            Assert.AreEqual(22, catalog.Definitions.Count);

            var reconstructed = new CharacterTestWorldObjectDefinitionCatalog(catalog.Definitions);
            Assert.AreEqual(catalog.Definitions.Count, reconstructed.Definitions.Count);
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                CharacterTestWorldObjectDefinition expected = catalog.Definitions[i];
                Assert.IsTrue(
                    reconstructed.TryGetDefinition(
                        expected.DefinitionId,
                        out CharacterTestWorldObjectDefinition byId));
                Assert.IsTrue(
                    reconstructed.TryGetDefinitionByStableId(
                        expected.StableId,
                        out CharacterTestWorldObjectDefinition byStableId));
                Assert.AreEqual(expected, byId);
                Assert.AreEqual(expected, byStableId);
            }
        }

        [Test]
        public void OfficialWorldObjectDocument_All54RowsResolveToExpectedDefinitionUsage()
        {
            string fullPath = ToFullPath(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            AssertOfficialWorldObjectDocumentBaseline(fullPath);
        }

        private static void AssertOfficialWorldObjectDocumentBaseline(string fullPath)
        {
            Assert.IsTrue(File.Exists(fullPath), "CharacterTest world object JSON should exist.");

            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument document,
                    out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));

            CharacterTestWorldObjectDefinitionCatalog catalog =
                CharacterTestWorldObjectSceneSource.BuiltInCatalog;
            int resolvedRows = 0;
            AssertInstanceDefinitions(
                document.MovableInstances,
                LevelAuthoringDefinitionUsage.Movable,
                "movableInstances",
                catalog,
                ref resolvedRows);
            AssertInstanceDefinitions(
                document.SingleStaticColliders,
                LevelAuthoringDefinitionUsage.StaticCollider,
                "singleStaticColliders",
                catalog,
                ref resolvedRows);
            AssertBatchDefinitions(document.StaticColliderBatches, catalog, ref resolvedRows);
            AssertPointDefinitions(document.Points, catalog, ref resolvedRows);
            AssertRegionDefinitions(document.Regions, catalog, ref resolvedRows);

            // M4-T2 adds three formal slope rows to the previous 51-row baseline.
            Assert.AreEqual(54, resolvedRows, "The official baseline must cover every current level row.");
        }

        private static string[] BuildDefinitionSnapshot(
            IReadOnlyList<CharacterTestWorldObjectDefinition> definitions)
        {
            var rows = new string[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
                rows[i] = BuildDefinitionSnapshotRow(definitions[i]);
            return rows;
        }

        private static string BuildDefinitionSnapshotRow(CharacterTestWorldObjectDefinition definition)
        {
            CharacterTestGenesisPotencyProfile profile = definition.Potency.Profile;
            var builder = new StringBuilder(320);
            Append(builder, definition.DefinitionId)
                .Append('|').Append(definition.StableId)
                .Append('|').Append(definition.DisplayName)
                .Append('|').Append(definition.ObjectKind)
                .Append('|').Append(definition.Category)
                .Append("|material=").Append(definition.Material.Kind);
            Append(builder.Append(','), definition.Material.Friction.RawValue);
            Append(builder.Append(','), definition.Material.Elasticity.RawValue);
            Append(builder.Append(','), definition.Material.FixedBounceHeight.RawValue);
            builder.Append("|shape=").Append(definition.Shape.ShapeKind);
            Append(builder.Append(','), definition.Shape.HalfExtentXMillimeters);
            Append(builder.Append(','), definition.Shape.HalfExtentYMillimeters);
            Append(builder.Append(','), definition.Shape.HalfExtentZMillimeters);
            builder.Append("|potency=");
            Append(builder, definition.Potency.BasePotency.Motion);
            Append(builder.Append(','), definition.Potency.BasePotency.Cohesion);
            Append(builder.Append(','), definition.Potency.BasePotency.Scatter);
            AppendBoolean(builder.Append(';'), profile.ImmuneMotion);
            AppendBoolean(builder.Append(','), profile.ImmuneCohesion);
            AppendBoolean(builder.Append(','), profile.ImmuneScatter);
            AppendBoolean(builder.Append(','), profile.BurningEnabled);
            AppendBoolean(builder.Append(','), profile.CrushEnabled);
            AppendBoolean(builder.Append(','), profile.CrackEnabled);
            AppendBoolean(builder.Append(','), profile.CondensedLayerEnabled);
            Append(builder.Append(','), profile.BurningMinMotion);
            Append(builder.Append(','), profile.BurningMinScatter);
            Append(builder.Append(','), profile.BurningMaxCohesion);
            Append(builder.Append(','), profile.CondensedLayerMinCohesion);
            Append(builder.Append(','), profile.CondensedLayerMaxMotion);
            builder.Append("|movable=");
            AppendBoolean(builder, definition.MovableNaturalObject.IsEnabled);
            Append(builder.Append(','), definition.MovableNaturalObject.MassGrams);
            builder.Append("|static=");
            AppendBoolean(builder, definition.StaticCollider.IsEnabled);
            Append(builder.Append(','), definition.StaticCollider.CombatLayer);
            Append(builder.Append(','), definition.StaticCollider.ColliderId);
            builder.Append("|point=");
            AppendBoolean(builder, definition.Point.IsEnabled);
            builder.Append(',').Append(definition.Point.PointKind);
            Append(builder.Append(','), definition.Point.CharacterId);
            Append(builder.Append(','), definition.Point.TeamId);
            builder.Append(',').Append(definition.Point.ControlOwner);
            builder.Append("|region=");
            AppendBoolean(builder, definition.Region.IsEnabled);
            builder.Append(',').Append(definition.Region.RegionKind);
            return builder.ToString();
        }

        private static StringBuilder Append(StringBuilder builder, int value)
        {
            return builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static StringBuilder Append(StringBuilder builder, long value)
        {
            return builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static StringBuilder AppendBoolean(StringBuilder builder, bool value)
        {
            return builder.Append(value ? '1' : '0');
        }

        private static void AssertInstanceDefinitions(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> rows,
            LevelAuthoringDefinitionUsage expectedUsage,
            string section,
            CharacterTestWorldObjectDefinitionCatalog catalog,
            ref int resolvedRows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                LevelWorldObjectInstanceDefinition row = rows[i];
                AssertDefinition(row.DefinitionId, expectedUsage, section + "[" + i + "]", catalog);
                resolvedRows++;
            }
        }

        private static void AssertBatchDefinitions(
            IReadOnlyList<LevelWorldObjectStaticColliderBatchDefinition> rows,
            CharacterTestWorldObjectDefinitionCatalog catalog,
            ref int resolvedRows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                AssertDefinition(
                    rows[i].DefinitionId,
                    LevelAuthoringDefinitionUsage.StaticCollider,
                    "staticColliderBatches[" + i + "]",
                    catalog);
                resolvedRows++;
            }
        }

        private static void AssertPointDefinitions(
            IReadOnlyList<LevelWorldObjectPointDefinition> rows,
            CharacterTestWorldObjectDefinitionCatalog catalog,
            ref int resolvedRows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                AssertDefinition(
                    rows[i].DefinitionId,
                    LevelAuthoringDefinitionUsage.Point,
                    "points[" + i + "]",
                    catalog);
                resolvedRows++;
            }
        }

        private static void AssertRegionDefinitions(
            IReadOnlyList<LevelWorldObjectRegionDefinition> rows,
            CharacterTestWorldObjectDefinitionCatalog catalog,
            ref int resolvedRows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                AssertDefinition(
                    rows[i].DefinitionId,
                    LevelAuthoringDefinitionUsage.Region,
                    "regions[" + i + "]",
                    catalog);
                resolvedRows++;
            }
        }

        private static void AssertDefinition(
            int definitionId,
            LevelAuthoringDefinitionUsage expectedUsage,
            string path,
            CharacterTestWorldObjectDefinitionCatalog catalog)
        {
            Assert.IsTrue(
                catalog.TryGetDefinition(definitionId, out LevelAuthoringDefinitionSummary summary),
                path + " references missing definition " + definitionId + ".");
            Assert.AreEqual(expectedUsage, summary.Usage, path + " resolved to the wrong usage.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.StableId), path + " has no stable definition id.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.DisplayName), path + " has no definition name.");
        }
    }
}
