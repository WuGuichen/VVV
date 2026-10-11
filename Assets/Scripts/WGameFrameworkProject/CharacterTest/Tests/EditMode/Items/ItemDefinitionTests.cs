using System;
using MxFramework.Config;
using MxFramework.Items;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class ItemDefinitionTests
    {
        [Test]
        public void ItemDefinitionSchema_ExposesCoreFields()
        {
            ConfigSchema<ItemDefinition> schema = ItemDefinition.CreateSchema();

            Assert.AreEqual(ItemDefinition.TableName, schema.TableName);
            AssertField(schema, nameof(ItemDefinition.Id));
            AssertField(schema, nameof(ItemDefinition.StableId));
            AssertField(schema, nameof(ItemDefinition.Kind));
            AssertField(schema, nameof(ItemDefinition.MaxStackCount));
            AssertField(schema, nameof(ItemDefinition.ResourceRefs));
            AssertField(schema, nameof(ItemDefinition.ProfileRefs));
        }

        [Test]
        public void BuiltInItemIds_KeepStableWireValues()
        {
            Assert.AreEqual("character.equipment", ItemProfileTypeIds.CharacterEquipment);
            Assert.AreEqual("items.generic", ItemProfileTypeIds.Generic);
            Assert.AreEqual("icon", ItemResourceUsageIds.Icon);
            Assert.AreEqual("mxframework.items.inventory", ItemRuntimeIds.InventoryModule);
            Assert.AreEqual("mxframework.items.world", ItemRuntimeIds.WorldItemsModule);
            Assert.AreEqual("mxframework.items.acquisition", ItemRuntimeIds.AcquisitionModule);
        }

        [Test]
        public void ItemDefinition_RejectsInvalidIdsAndStackPolicy()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(
                new ItemDefinitionId(0),
                "item.invalid",
                new LocalizedTextKey("item.invalid.name"),
                new LocalizedTextKey("item.invalid.desc"),
                ItemKind.Material,
                new ItemTagSet(null),
                new ItemStackPolicy(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStackPolicy(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(
                new ItemDefinitionId(1001),
                "item.invalid-stack-policy",
                new LocalizedTextKey("item.invalid-stack-policy.name"),
                new LocalizedTextKey("item.invalid-stack-policy.desc"),
                ItemKind.Material,
                new ItemTagSet(null),
                default));

            var equipment = new ItemDefinition(
                new ItemDefinitionId(2001),
                "item.default-equipment-stack-policy",
                new LocalizedTextKey("item.default-equipment-stack-policy.name"),
                new LocalizedTextKey("item.default-equipment-stack-policy.desc"),
                ItemKind.Equipment,
                new ItemTagSet(null),
                default);
            Assert.AreEqual(1, equipment.MaxStackCount);
        }

        [Test]
        public void ItemDefinitionRegistry_RejectsDuplicateIdAndStableId()
        {
            var registry = new ItemDefinitionRegistry();
            ItemDefinition herb = CreateDefinition(1001, "item.herb", ItemKind.Material, 20);
            ItemDefinition duplicateId = CreateDefinition(1001, "item.other", ItemKind.Material, 20);
            ItemDefinition duplicateStable = CreateDefinition(1002, "item.herb", ItemKind.Material, 20);

            Assert.IsTrue(registry.Register(herb).Success);
            Assert.AreEqual(ItemDefinitionRegistryStatus.DuplicateId, registry.Register(duplicateId).Status);
            Assert.AreEqual(ItemDefinitionRegistryStatus.DuplicateStableId, registry.Register(duplicateStable).Status);
        }

        [Test]
        public void ItemDefinitionRegistry_StableHashIgnoresRegistrationOrder()
        {
            var left = new ItemDefinitionRegistry();
            left.Register(CreateDefinition(1002, "item.ore", ItemKind.Material, 30));
            left.Register(CreateDefinition(1001, "item.herb", ItemKind.Material, 20));

            var right = new ItemDefinitionRegistry();
            right.Register(CreateDefinition(1001, "item.herb", ItemKind.Material, 20));
            right.Register(CreateDefinition(1002, "item.ore", ItemKind.Material, 30));

            Assert.AreEqual(left.ComputeStableHash(), right.ComputeStableHash());
        }

        internal static ItemDefinition CreateDefinition(int id, string stableId, ItemKind kind, int maxStack)
        {
            return new ItemDefinition(
                new ItemDefinitionId(id),
                stableId,
                new LocalizedTextKey(stableId + ".name"),
                new LocalizedTextKey(stableId + ".desc"),
                kind,
                new ItemTagSet(new[] { 10, 10, 2 }),
                ItemStackPolicy.ForKind(kind, maxStack),
                new[] { new ItemResourceRef(new ResourceKey(stableId + ".icon", ResourceTypeIds.Sprite), ItemResourceUsageIds.Icon) },
                new[] { new ItemProfileRef(kind == ItemKind.Equipment ? ItemProfileTypeIds.CharacterEquipment : ItemProfileTypeIds.Generic, stableId + ".profile") });
        }

        private static void AssertField(ConfigSchema schema, string fieldName)
        {
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                if (schema.Fields[i].Name == fieldName)
                    return;
            }

            Assert.Fail("Missing schema field: " + fieldName);
        }
    }
}
