using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterApplication
{
    public class CharacterApplicationConfigSchemaTests
    {
        [Test]
        public void CreateAll_ReturnsTwelveCharacterSchemas()
        {
            IReadOnlyList<ConfigSchema> schemas = CharacterApplicationConfigSchemas.CreateAll();

            Assert.AreEqual(12, schemas.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    CharacterConfig.TableName,
                    CharacterAttributeProfileConfig.TableName,
                    CharacterBodyProfileConfig.TableName,
                    CharacterBodyPartConfig.TableName,
                    EquipmentSchemaConfig.TableName,
                    EquipmentLoadoutConfig.TableName,
                    EquipmentStateConfig.TableName,
                    WeaponConfig.TableName,
                    AbilityLoadoutConfig.TableName,
                    CombatActionSetConfig.TableName,
                    CharacterPresentationProfileConfig.TableName,
                    SpawnProfileConfig.TableName
                },
                schemas.Select(schema => schema.TableName).ToArray());
        }

        [Test]
        public void CharacterConfigSchema_DeclaresTypedReferences()
        {
            ConfigSchema<CharacterConfig> schema = CharacterConfig.CreateSchema();

            AssertReference(schema, "AttributeProfileId", CharacterAttributeProfileConfig.TableName, typeof(CharacterAttributeProfileId), true);
            AssertReference(schema, "BodyProfileId", CharacterBodyProfileConfig.TableName, typeof(CharacterBodyProfileId), true);
            AssertReference(schema, "EquipmentSchemaId", EquipmentSchemaConfig.TableName, typeof(EquipmentSchemaId), true);
            AssertReference(schema, "DefaultLoadoutId", EquipmentLoadoutConfig.TableName, typeof(EquipmentLoadoutId), true);
            AssertReference(schema, "BaseAbilityLoadoutId", AbilityLoadoutConfig.TableName, typeof(AbilityLoadoutId), false);
            AssertReference(schema, "PresentationProfileId", CharacterPresentationProfileConfig.TableName, typeof(CharacterPresentationProfileId), true);
        }

        [Test]
        public void AttributeEntry_UsesInitialValueNotRuntimeValue()
        {
            string[] propertyNames = typeof(CharacterAttributeEntry)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            CollectionAssert.Contains(propertyNames, "InitialValue");
            CollectionAssert.DoesNotContain(propertyNames, "Current" + "Value");
        }

        [Test]
        public void CombatActionEntry_DoesNotOwnTimelineAuthorityFields()
        {
            string[] propertyNames = typeof(CombatActionEntry)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(propertyNames, "DurationFrames");
            CollectionAssert.DoesNotContain(propertyNames, "HitStartFrame");
            CollectionAssert.DoesNotContain(propertyNames, "HitEndFrame");
            CollectionAssert.DoesNotContain(propertyNames, "CancelWindowStart");
            CollectionAssert.DoesNotContain(propertyNames, "CancelWindowEnd");
        }

        [Test]
        public void SourceIndex_ValidatesTypedIdCrossSourceReferences()
        {
            var table = new ConfigTable<CharacterConfig>(CharacterConfig.CreateSchema());
            table.Add(CreateCharacterConfig());

            ConfigSourceIndex sourceIndex = CreateCompleteSourceIndex();

            ConfigTableValidationReport report = sourceIndex.ValidateCrossSourceReferences(table);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void SourceIndex_WhenTypedReferenceTargetMissing_ReportsStableField()
        {
            var table = new ConfigTable<CharacterConfig>(CharacterConfig.CreateSchema());
            table.Add(CreateCharacterConfig());
            ConfigSourceIndex sourceIndex = CreateCompleteSourceIndex(registerPresentation: false);

            ConfigTableValidationReport report = sourceIndex.ValidateCrossSourceReferences(table);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual("PresentationProfileId", report.Issues[0].FieldName);
            Assert.AreEqual(ConfigError.TypeNotRegistered, report.Issues[0].Error);
        }

        [Test]
        public void Schemas_DoNotExposeUnityObjectFields()
        {
            IReadOnlyList<ConfigSchema> schemas = CharacterApplicationConfigSchemas.CreateAll();
            for (int i = 0; i < schemas.Count; i++)
            {
                ConfigSchema schema = schemas[i];
                for (int j = 0; j < schema.Fields.Count; j++)
                {
                    ConfigField field = schema.Fields[j];
                    if (field.ValueType == null)
                        continue;

                    string fullName = field.ValueType.FullName ?? string.Empty;
                    Assert.IsFalse(fullName.StartsWith("UnityEngine.", System.StringComparison.Ordinal), schema.TableName + "." + field.Name);
                    Assert.IsFalse(fullName.Contains("AnimationClip"), schema.TableName + "." + field.Name);
                    Assert.IsFalse(fullName.Contains("GameObject"), schema.TableName + "." + field.Name);
                    Assert.IsFalse(fullName.Contains("Material"), schema.TableName + "." + field.Name);
                }
            }
        }

        private static CharacterConfig CreateCharacterConfig()
        {
            return new CharacterConfig(
                new CharacterConfigId(710001),
                "mx.character.iron_vanguard",
                new LocalizedTextKey("character.iron_vanguard.name"),
                new LocalizedTextKey("character.iron_vanguard.desc"),
                new CharacterAttributeProfileId(720001),
                new CharacterBodyProfileId(730001),
                new EquipmentSchemaId(750001),
                new EquipmentLoadoutId(760001),
                new AbilityLoadoutId(0),
                new CharacterPresentationProfileId(810001),
                CharacterControllerKind.HumanInput,
                "controller.human.default",
                new[] { "sample", "vanguard" });
        }

        private static ConfigSourceIndex CreateCompleteSourceIndex(bool registerPresentation = true)
        {
            var sourceIndex = new ConfigSourceIndex();
            sourceIndex.Register(new ConfigSourceEntry(CharacterAttributeProfileConfig.CreateSchema()).AddKey(720001));
            sourceIndex.Register(new ConfigSourceEntry(CharacterBodyProfileConfig.CreateSchema()).AddKey(730001));
            sourceIndex.Register(new ConfigSourceEntry(EquipmentSchemaConfig.CreateSchema()).AddKey(750001));
            sourceIndex.Register(new ConfigSourceEntry(EquipmentLoadoutConfig.CreateSchema()).AddKey(760001));
            sourceIndex.Register(new ConfigSourceEntry(AbilityLoadoutConfig.CreateSchema()).AddKey(790001));

            if (registerPresentation)
                sourceIndex.Register(new ConfigSourceEntry(CharacterPresentationProfileConfig.CreateSchema()).AddKey(810001));

            return sourceIndex;
        }

        private static void AssertReference(
            ConfigSchema schema,
            string fieldName,
            string targetTable,
            System.Type valueType,
            bool required)
        {
            ConfigField field = schema.Fields.First(item => item.Name == fieldName);
            Assert.AreEqual(ConfigFieldType.ConfigReference, field.FieldType);
            Assert.AreEqual(valueType, field.ValueType);
            Assert.AreEqual(targetTable, field.ReferenceRule.TargetSchemaName);
            Assert.AreEqual(ConfigStructureKind.Table, field.ReferenceRule.TargetStructureKind);
            Assert.AreEqual(required, field.ReferenceRule.Required);
        }
    }
}
