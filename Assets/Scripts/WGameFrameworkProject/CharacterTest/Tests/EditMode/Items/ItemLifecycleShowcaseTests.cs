using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Demo.Items;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Items
{
    public sealed class ItemLifecycleShowcaseTests
    {
        private const string UxmlPath = "Assets/UI/MxFramework/ItemLifecycle/ItemLifecycleShowcase.uxml";
        private const string UssPath = "Assets/UI/MxFramework/ItemLifecycle/ItemLifecycleShowcase.uss";
        private const string PanelSettingsPath = "Assets/UI/MxFramework/ItemLifecycle/ItemLifecycleShowcasePanelSettings.asset";
        private const string IronSwordIconPath = "Assets/UI/MxFramework/ItemLifecycle/Icons/iron_sword_icon.png";

        [Test]
        public void Showcase_CommandPathFlowsLootWorldInventoryAndEquipment()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                ItemLifecycleShowcaseSnapshot initial = showcase.CreateSnapshot();
                Assert.AreEqual(0, initial.WorldItemCount);
                Assert.AreEqual(2, initial.EquipmentSlots.Length);
                Assert.AreEqual("Main Hand", initial.SelectedEquipmentSlotName);
                Assert.IsTrue(initial.EquipmentSlots[0].IsSelected);

                showcase.OpenChest();
                ItemLifecycleShowcaseSnapshot afterLoot = showcase.CreateSnapshot();
                Assert.AreEqual(2, afterLoot.WorldItemCount);
                AssertIronSwordIcon(FindWorldItem(afterLoot, "Iron Sword").Icon);

                showcase.PickupNearest();
                ItemLifecycleShowcaseSnapshot afterFirstPickup = showcase.CreateSnapshot();
                Assert.AreEqual(1, afterFirstPickup.WorldItemCount);
                Assert.IsFalse(afterFirstPickup.InventorySlots[0].IsEmpty);

                showcase.Move(1, 0);
                showcase.PickupNearest();
                showcase.EquipFirstWeapon();
                ItemLifecycleShowcaseSnapshot equipped = showcase.CreateSnapshot();

                Assert.AreEqual(0, equipped.WorldItemCount);
                Assert.AreEqual("Main Hand: Iron Sword", equipped.EquipmentProfile);
                Assert.IsFalse(equipped.EquipmentSlots[0].IsEmpty);
                AssertIronSwordIcon(equipped.EquipmentSlots[0].Icon);
                Assert.AreNotEqual(0L, equipped.StateHash);
            }
        }

        [Test]
        public void Showcase_EquipWithoutWeaponLeavesStateUnchanged()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                long beforeHash = showcase.CreateSnapshot().StateHash;

                showcase.EquipFirstWeapon();
                ItemLifecycleShowcaseSnapshot snapshot = showcase.CreateSnapshot();

                Assert.AreEqual(beforeHash, snapshot.StateHash);
                Assert.AreEqual("None", snapshot.EquipmentProfile);
                Assert.IsTrue(AllSlotsEmpty(snapshot.EquipmentSlots));
                AssertLogContains(snapshot, "Equip skipped: no weapon in bag");
            }
        }

        [Test]
        public void Showcase_UnequipEmptySelectedSlotLeavesStateUnchanged()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                long beforeHash = showcase.CreateSnapshot().StateHash;

                showcase.UnequipSelectedSlot();
                ItemLifecycleShowcaseSnapshot snapshot = showcase.CreateSnapshot();

                Assert.AreEqual(beforeHash, snapshot.StateHash);
                Assert.AreEqual("None", snapshot.EquipmentProfile);
                Assert.IsTrue(AllSlotsEmpty(snapshot.EquipmentSlots));
                AssertLogContains(snapshot, "Unequip skipped: Main Hand empty");
            }
        }

        [Test]
        public void Showcase_EquipTargetsSelectedSlotAndUnequipReturnsToBag()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                showcase.OpenChest();
                showcase.PickupNearest();
                showcase.Move(1, 0);
                showcase.PickupNearest();

                showcase.SelectEquipmentSlot(1);
                ItemLifecycleShowcaseSnapshot selected = showcase.CreateSnapshot();
                Assert.AreEqual(1, selected.SelectedEquipmentSlotIndex);
                Assert.AreEqual("Off Hand", selected.SelectedEquipmentSlotName);
                Assert.IsTrue(selected.EquipmentSlots[1].IsSelected);

                showcase.EquipFirstWeapon();
                ItemLifecycleShowcaseSnapshot equipped = showcase.CreateSnapshot();
                Assert.IsTrue(equipped.EquipmentSlots[0].IsEmpty);
                Assert.IsFalse(equipped.EquipmentSlots[1].IsEmpty);
                Assert.AreEqual("Iron Sword", equipped.EquipmentSlots[1].ItemName);
                Assert.AreEqual("Off Hand: Iron Sword", equipped.EquipmentProfile);
                AssertLogContains(equipped, "Equipped Iron Sword -> Off Hand");

                showcase.UnequipSelectedSlot();
                ItemLifecycleShowcaseSnapshot unequipped = showcase.CreateSnapshot();
                Assert.IsTrue(unequipped.EquipmentSlots[1].IsEmpty);
                AssertIronSwordIcon(FindInventorySlot(unequipped, "Iron Sword").Icon);
                AssertLogContains(unequipped, "Unequipped Iron Sword -> bag slot");
            }
        }

        [Test]
        public void Showcase_RepeatEquipToOccupiedSlotKeepsEquipmentAndBagState()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                CollectSwordFromNewChest(showcase);
                showcase.EquipFirstWeapon();
                CollectSwordFromNewChest(showcase);
                ItemLifecycleShowcaseSnapshot beforeRepeat = showcase.CreateSnapshot();
                long beforeHash = beforeRepeat.StateHash;
                Assert.AreEqual("Main Hand: Iron Sword", beforeRepeat.EquipmentProfile);
                Assert.AreEqual(1, CountInventorySlotsWithItem(beforeRepeat, "Iron Sword"));

                showcase.EquipFirstWeapon();
                ItemLifecycleShowcaseSnapshot afterRepeat = showcase.CreateSnapshot();

                Assert.AreEqual(beforeHash, afterRepeat.StateHash);
                Assert.AreEqual("Main Hand: Iron Sword", afterRepeat.EquipmentProfile);
                Assert.AreEqual("Iron Sword", afterRepeat.EquipmentSlots[0].ItemName);
                Assert.AreEqual(1, CountInventorySlotsWithItem(afterRepeat, "Iron Sword"));
                AssertLogContains(afterRepeat, "Equip skipped: Main Hand occupied");
            }
        }

        [Test]
        public void Showcase_InvalidEquipmentSlotSelectionIsRejected()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                showcase.SelectEquipmentSlot(1);
                Assert.AreEqual("Off Hand", showcase.CreateSnapshot().SelectedEquipmentSlotName);

                showcase.SelectEquipmentSlot(-1);
                showcase.SelectEquipmentSlot(99);
                ItemLifecycleShowcaseSnapshot snapshot = showcase.CreateSnapshot();

                Assert.AreEqual(1, snapshot.SelectedEquipmentSlotIndex);
                Assert.AreEqual("Off Hand", snapshot.SelectedEquipmentSlotName);
                Assert.IsTrue(snapshot.EquipmentSlots[1].IsSelected);
                AssertLogContains(snapshot, "Select slot skipped: invalid equipment slot -1");
                AssertLogContains(snapshot, "Select slot skipped: invalid equipment slot 99");
            }
        }

        [Test]
        public void Showcase_UnequipWithFullBagFailsAndKeepsEquipment()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                CollectSwordFromNewChest(showcase);
                showcase.EquipFirstWeapon();
                FillBagWithLoot(showcase);
                ItemLifecycleShowcaseSnapshot fullBag = showcase.CreateSnapshot();
                Assert.AreEqual(0, CountEmptyInventorySlots(fullBag));
                Assert.AreEqual("Main Hand: Iron Sword", fullBag.EquipmentProfile);

                showcase.UnequipSelectedSlot();
                ItemLifecycleShowcaseSnapshot afterUnequip = showcase.CreateSnapshot();

                Assert.AreEqual(0, CountEmptyInventorySlots(afterUnequip));
                Assert.AreEqual("Main Hand: Iron Sword", afterUnequip.EquipmentProfile);
                Assert.AreEqual("Iron Sword", afterUnequip.EquipmentSlots[0].ItemName);
                AssertLogContains(afterUnequip, "Unequip failed: bag full");
            }
        }

        [Test]
        public void Showcase_ResetRestoresDefaultTargetAndEmptyState()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                CollectSwordFromNewChest(showcase);
                showcase.SelectEquipmentSlot(1);
                showcase.EquipFirstWeapon();
                Assert.IsTrue(showcase.Save());

                showcase.Reset();
                ItemLifecycleShowcaseSnapshot snapshot = showcase.CreateSnapshot();

                Assert.IsFalse(showcase.HasSavedState);
                Assert.AreEqual(0, snapshot.WorldItemCount);
                Assert.AreEqual("Main Hand", snapshot.SelectedEquipmentSlotName);
                Assert.IsTrue(snapshot.EquipmentSlots[0].IsSelected);
                Assert.AreEqual("None", snapshot.EquipmentProfile);
                Assert.IsTrue(AllSlotsEmpty(snapshot.InventorySlots));
                Assert.IsTrue(AllSlotsEmpty(snapshot.EquipmentSlots));
                Assert.AreEqual("No save", snapshot.SaveStatus);
            }
        }

        [Test]
        public void Showcase_SaveLoadRestoresInventoryAndWorldStateHash()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                showcase.OpenChest();
                Assert.IsTrue(showcase.Save());
                long savedHash = showcase.CreateSnapshot().StateHash;

                showcase.PickupNearest();
                Assert.AreNotEqual(savedHash, showcase.CreateSnapshot().StateHash);

                Assert.IsTrue(showcase.Load());
                Assert.AreEqual(savedHash, showcase.CreateSnapshot().StateHash);
            }
        }

        [Test]
        public void Showcase_SaveLoadRestoresEquipmentAndWorldButDoesNotRestoreSelectedSlot()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                CollectSwordFromNewChest(showcase);
                showcase.SelectEquipmentSlot(1);
                showcase.EquipFirstWeapon();
                showcase.OpenChest();
                Assert.IsTrue(showcase.Save());
                long savedHash = showcase.CreateSnapshot().StateHash;

                showcase.SelectEquipmentSlot(0);
                Assert.AreEqual(savedHash, showcase.CreateSnapshot().StateHash);
                showcase.PickupNearest();
                Assert.AreNotEqual(savedHash, showcase.CreateSnapshot().StateHash);

                Assert.IsTrue(showcase.Load());
                ItemLifecycleShowcaseSnapshot restored = showcase.CreateSnapshot();

                Assert.AreEqual(savedHash, restored.StateHash);
                Assert.AreEqual("Off Hand: Iron Sword", restored.EquipmentProfile);
                Assert.AreEqual("Iron Sword", restored.EquipmentSlots[1].ItemName);
                Assert.AreEqual("Main Hand", restored.SelectedEquipmentSlotName);
            }
        }

        [Test]
        public void Showcase_LogsOutOfRangePickupAndMoveMakesIronSwordReachable()
        {
            using (var showcase = new ItemLifecycleShowcase())
            {
                showcase.OpenChest();
                showcase.PickupNearest();

                ItemLifecycleShowcaseSnapshot afterFirstPickup = showcase.CreateSnapshot();
                Assert.AreEqual(1, afterFirstPickup.WorldItemCount);
                Assert.AreEqual("Iron Sword", afterFirstPickup.WorldItems[0].ItemName);
                AssertIronSwordIcon(afterFirstPickup.WorldItems[0].Icon);
                Assert.AreEqual(2, afterFirstPickup.WorldItems[0].Distance);
                Assert.IsFalse(afterFirstPickup.WorldItems[0].InRange);

                showcase.PickupNearest();
                ItemLifecycleShowcaseSnapshot skipped = showcase.CreateSnapshot();
                Assert.AreEqual(1, skipped.WorldItemCount);
                AssertLogContains(skipped, "Pickup skipped: nearest");
                AssertLogContains(skipped, "Iron Sword");
                AssertLogContains(skipped, "d=2 range=1");

                showcase.Move(1, 0);
                ItemLifecycleShowcaseSnapshot moved = showcase.CreateSnapshot();
                Assert.AreEqual(1, moved.WorldItems[0].Distance);
                Assert.IsTrue(moved.WorldItems[0].InRange);
                AssertLogContains(moved, "Move 1,0");

                showcase.PickupNearest();
                ItemLifecycleShowcaseSnapshot pickedSword = showcase.CreateSnapshot();
                Assert.AreEqual(0, pickedSword.WorldItemCount);
                AssertIronSwordIcon(FindInventorySlot(pickedSword, "Iron Sword").Icon);
                AssertLogContains(pickedSword, "Pickup target: #2 Iron Sword");
            }
        }

        [Test]
        public void ShowcaseRunner_UiButtonsAndEquipmentSlotClickDriveState()
        {
            GameObject runnerObject = null;
            try
            {
                runnerObject = new GameObject("ItemLifecycleShowcaseRunnerUiSmokeTest");
                runnerObject.SetActive(false);
                PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
                StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
                UIDocument document = runnerObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = visualTree;
                ItemLifecycleShowcaseRunner runner = runnerObject.AddComponent<ItemLifecycleShowcaseRunner>();
                runner.ConfigureAssets(
                    document,
                    panelSettings,
                    visualTree,
                    styleSheet,
                    AssetDatabase.LoadAssetAtPath<Sprite>(IronSwordIconPath));

                runnerObject.SetActive(true);
                runner.enabled = false;
                runner.enabled = true;
                InvokeRunnerUpdate(runner);

                VisualElement root = document.rootVisualElement.Q<VisualElement>("item-lifecycle-root");
                Assert.IsNotNull(root);
                Assert.IsNotNull(root.Q<Button>("open-button"));
                Assert.IsNotNull(root.Q<Button>("pickup-button"));
                Assert.IsNotNull(root.Q<Button>("equip-button"));
                Assert.IsNotNull(root.Q<Button>("unequip-button"));
                Assert.IsNotNull(root.Q<Label>("target-slot-label"));
                Assert.IsNotNull(root.Q<VisualElement>("inventory-grid"));
                Assert.IsNotNull(root.Q<VisualElement>("equipment-grid"));

                var buttons = new List<Button>();
                root.Query<Button>(className: "command-button").ForEach(buttons.Add);
                Assert.Greater(buttons.Count, 0);
                for (int i = 0; i < buttons.Count; i++)
                    Assert.IsFalse(buttons[i].focusable, buttons[i].name + " should not capture keyboard focus.");

                PressButton(root.Q<Button>("open-button"));
                Assert.AreEqual("2", root.Q<Label>("world-count-label").text);
                PressButton(root.Q<Button>("pickup-button"));
                PressButton(root.Q<Button>("move-right-button"));
                PressButton(root.Q<Button>("pickup-button"));

                VisualElement equipmentGrid = root.Q<VisualElement>("equipment-grid");
                Assert.AreEqual(2, equipmentGrid.childCount);
                Assert.IsTrue(equipmentGrid.ElementAt(0).ClassListContains("slot-clickable"));
                Assert.IsTrue(equipmentGrid.ElementAt(1).ClassListContains("slot-clickable"));
                Assert.AreEqual("Main Hand", root.Q<Label>("target-slot-label").text);

                PressButton(root.Q<Button>("equip-button"));
                Assert.AreEqual("Main Hand: Iron Sword", root.Q<Label>("equipment-label").text);
                Assert.That(SlotValueText(equipmentGrid.ElementAt(0)), Does.Contain("Iron Sword"));

                PressButton(root.Q<Button>("unequip-button"));
                Assert.AreEqual("None", root.Q<Label>("equipment-label").text);
                Assert.AreEqual("Empty", SlotValueText(equipmentGrid.ElementAt(0)));
                Assert.IsTrue(GridContainsSlotValue(root.Q<VisualElement>("inventory-grid"), "Iron Sword"));
            }
            finally
            {
                if (runnerObject != null)
                    UnityEngine.Object.DestroyImmediate(runnerObject);
            }
        }

        [Test]
        public void Showcase_IconResourceManagerLoadsIronSwordSprite()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Sprite sprite = null;
            ResourceHandle<Sprite> handle = null;
            ResourceManager manager = null;
            try
            {
                texture.SetPixels(new[]
                {
                    Color.clear,
                    Color.white,
                    Color.gray,
                    Color.clear
                });
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);

                manager = ItemLifecycleShowcaseRunner.CreateIconResourceManager(sprite);
                ResourceLoadResult<ResourceHandle<Sprite>> load = manager.Load<Sprite>(ItemLifecycleShowcaseRunner.IronSwordIconKey);

                Assert.IsTrue(load.Success, load.Error.Message);
                handle = load.Value;
                Assert.AreSame(sprite, handle.Value);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
            }
            finally
            {
                if (manager != null && handle != null && !handle.IsReleased)
                    manager.Release(handle);
                if (manager != null)
                    manager.ReleaseAllLoaded("testCleanup");
                if (sprite != null)
                    UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Showcase_IconResourceManagerDoesNotExposeNonCatalogIconData()
        {
            ResourceCatalog catalog = ItemLifecycleShowcaseRunner.CreateIconCatalog();
            Assert.AreEqual(1, catalog.Entries.Count);
            Assert.AreEqual(ItemLifecycleShowcaseRunner.IronSwordIconResourceId, catalog.Entries[0].Id);
            Assert.AreEqual(ResourceTypeIds.Sprite, catalog.Entries[0].TypeId);
            Assert.AreEqual(ItemLifecycleShowcaseRunner.IronSwordIconAddress, catalog.Entries[0].Address);
        }

        private static void CollectSwordFromNewChest(ItemLifecycleShowcase showcase)
        {
            MoveToOrigin(showcase);
            showcase.OpenChest();
            showcase.PickupNearest();
            showcase.Move(1, 0);
            showcase.PickupNearest();
        }

        private static void FillBagWithLoot(ItemLifecycleShowcase showcase)
        {
            for (int i = 0; i < 12 && CountEmptyInventorySlots(showcase.CreateSnapshot()) > 0; i++)
                CollectSwordFromNewChest(showcase);

            Assert.AreEqual(0, CountEmptyInventorySlots(showcase.CreateSnapshot()), "Expected the public loot/pickup path to fill all bag slots.");
        }

        private static void MoveToOrigin(ItemLifecycleShowcase showcase)
        {
            ItemLifecycleShowcaseSnapshot snapshot = showcase.CreateSnapshot();
            while (snapshot.PlayerX > 0)
            {
                showcase.Move(-1, 0);
                snapshot = showcase.CreateSnapshot();
            }

            while (snapshot.PlayerX < 0)
            {
                showcase.Move(1, 0);
                snapshot = showcase.CreateSnapshot();
            }
        }

        private static void AssertLogContains(ItemLifecycleShowcaseSnapshot snapshot, string expected)
        {
            for (int i = 0; i < snapshot.Log.Length; i++)
            {
                if (snapshot.Log[i].Contains(expected))
                    return;
            }

            Assert.Fail("Expected log to contain: " + expected);
        }

        private static bool AllSlotsEmpty(ItemLifecycleSlotSnapshot[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                    return false;
            }

            return true;
        }

        private static int CountEmptyInventorySlots(ItemLifecycleShowcaseSnapshot snapshot)
        {
            int count = 0;
            for (int i = 0; i < snapshot.InventorySlots.Length; i++)
            {
                if (snapshot.InventorySlots[i].IsEmpty)
                    count++;
            }

            return count;
        }

        private static int CountInventorySlotsWithItem(ItemLifecycleShowcaseSnapshot snapshot, string itemName)
        {
            int count = 0;
            for (int i = 0; i < snapshot.InventorySlots.Length; i++)
            {
                if (snapshot.InventorySlots[i].ItemName == itemName)
                    count++;
            }

            return count;
        }

        private static void AssertIronSwordIcon(ItemLifecycleIconSnapshot icon)
        {
            Assert.IsTrue(icon.IsValid);
            Assert.AreEqual(ItemLifecycleShowcaseRunner.IronSwordIconResourceId, icon.ResourceId);
            Assert.AreEqual(ResourceTypeIds.Sprite, icon.ResourceTypeId);
        }

        private static ItemLifecycleSlotSnapshot FindInventorySlot(ItemLifecycleShowcaseSnapshot snapshot, string itemName)
        {
            for (int i = 0; i < snapshot.InventorySlots.Length; i++)
            {
                if (snapshot.InventorySlots[i].ItemName == itemName)
                    return snapshot.InventorySlots[i];
            }

            Assert.Fail("Expected inventory slot for item: " + itemName);
            return default;
        }

        private static ItemLifecycleWorldItemSnapshot FindWorldItem(ItemLifecycleShowcaseSnapshot snapshot, string itemName)
        {
            for (int i = 0; i < snapshot.WorldItems.Length; i++)
            {
                if (snapshot.WorldItems[i].ItemName == itemName)
                    return snapshot.WorldItems[i];
            }

            Assert.Fail("Expected world item: " + itemName);
            return default;
        }

        private static bool GridContainsSlotValue(VisualElement grid, string expected)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                if (SlotValueText(grid.ElementAt(i)).Contains(expected))
                    return true;
            }

            return false;
        }

        private static string SlotValueText(VisualElement slot)
        {
            Label value = slot.Q<Label>(className: "slot-value");
            Assert.IsNotNull(value);
            return value.text;
        }

        private static void PressButton(Button button)
        {
            Assert.IsNotNull(button);
            object clickable = typeof(Button)
                .GetProperty("clickable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.IsNotNull(clickable, "Button clickable was not available.");

            MethodInfo invoke = FindClickableInvoke(clickable.GetType());
            Assert.IsNotNull(invoke, "Clickable invoke method was not available.");

            ParameterInfo[] parameters = invoke.GetParameters();
            if (parameters.Length == 0)
            {
                invoke.Invoke(clickable, Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
                invoke.Invoke(clickable, new object[] { click });
        }

        private static void InvokeRunnerUpdate(ItemLifecycleShowcaseRunner runner)
        {
            MethodInfo update = typeof(ItemLifecycleShowcaseRunner)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(update);
            update.Invoke(runner, Array.Empty<object>());
        }

        private static MethodInfo FindClickableInvoke(Type clickableType)
        {
            MethodInfo[] methods = clickableType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Invoke", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    return method;
                if (parameters.Length == 1 && typeof(EventBase).IsAssignableFrom(parameters[0].ParameterType))
                    return method;
            }

            return null;
        }
    }
}
