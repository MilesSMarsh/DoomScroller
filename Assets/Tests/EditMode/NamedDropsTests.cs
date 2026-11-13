using System.Collections.Generic;
using System.Linq;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class NamedDropsTests
    {
        private static readonly string[] SlotNames =
            System.Enum.GetNames(typeof(EquipmentSlotType));

        [Test]
        public void RollLootTable_EmptyPools_ReturnsOnlyNamedAuthoredItems()
        {
            var table = new LootTable { minItemCount = 200, maxItemCount = 200, percentByRarity = new float[] { 100, 0, 0, 0, 0 } };
            var equipmentNames = Resources.LoadAll<EquipmentDefinition>("EquipmentDefinitions")
                .Select(d => d.equipmentName).ToList();

            var items = RewardGenerator.RollLootTable(table, new System.Random(1));

            Assert.AreEqual(200, items.Length);
            foreach (var item in items)
            {
                foreach (var slot in SlotNames)
                {
                    Assert.AreNotEqual($"{item.rarity} {slot}", item.title);
                }

                if (item.category == ItemCategory.Weapon)
                {
                    Assert.IsNotNull(item.sourceDefinition, item.title);
                    Assert.AreEqual(WeaponGenerator.TitleFor(item.rarity, item.sourceDefinition.weaponName), item.title);
                }
                else
                {
                    Assert.IsTrue(equipmentNames.Contains(item.title), item.title);
                }
            }
        }

        [Test]
        public void GenerateItem_ShieldCategory_UsesAuthoredDefinition()
        {
            var names = Resources.LoadAll<EquipmentDefinition>("EquipmentDefinitions")
                .Where(d => d.category == ItemCategory.Shield).Select(d => d.equipmentName).ToList();
            Assert.IsNotEmpty(names);

            for (var i = 0; i < 20; i++)
            {
                var item = RewardGenerator.GenerateItem(ItemRarity.Plain, new System.Random(i),
                    new[] { ItemCategory.Shield });
                Assert.AreEqual(ItemCategory.Shield, item.category);
                Assert.IsTrue(names.Contains(item.title), item.title);
                Assert.IsNotNull(item.equipmentSource);
            }
        }

        [Test]
        public void GeneratedWeapons_DefaultToRightHand()
        {
            Assert.AreEqual(EquipmentSlotType.RightHand, WeaponGenerator.Generate(ItemRarity.Plain, new System.Random(1)).slotType);
            var named = WeaponGenerator.GenerateFromRandomDefinition(ItemRarity.Plain, new System.Random(1));
            Assert.AreEqual(EquipmentSlotType.RightHand, named.slotType);
        }

        [Test]
        public void Equip_WeaponWithHeadSlotType_GoesToAllowedHand()
        {
            var sword = WeaponGenerator.Generate(ItemRarity.Plain, new System.Random(1));
            sword.slotType = EquipmentSlotType.Head;
            var loadout = new PlayerLoadout(new List<ItemData> { sword });

            loadout.Equip(sword);

            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.Head));
            Assert.AreSame(sword, loadout.GetEquipped(sword.slotType));
            Assert.Contains(sword.slotType, ItemCategoryInfo.SlotsFor(ItemCategory.Weapon));
        }

        [Test]
        public void GenerateItem_WeaponCategory_UsesAuthoredWeaponDefinition()
        {
            for (var i = 0; i < 20; i++)
            {
                var item = RewardGenerator.GenerateItem(ItemRarity.Plain, new System.Random(i),
                    new[] { ItemCategory.Weapon });
                Assert.AreEqual(ItemCategory.Weapon, item.category);
                Assert.IsNotNull(item.sourceDefinition, item.title);
                Assert.AreEqual(EquipmentSlotType.RightHand, item.slotType, item.title);
            }
        }

        [Test]
        public void Equip_ShieldWithStaleHeadSlot_GoesToFirstAllowedHand()
        {
            var shield = MakeItem(ItemCategory.Shield, EquipmentSlotType.Head);
            var loadout = new PlayerLoadout(new List<ItemData> { shield });

            loadout.Equip(shield);

            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.Head));
            Assert.AreSame(shield, loadout.GetEquipped(EquipmentSlotType.LeftHand));
        }

        [Test]
        public void Equip_ArmorWithStaleHandSlot_GoesToAllowedBodySlot()
        {
            var armor = MakeItem(ItemCategory.Armor, EquipmentSlotType.RightHand);
            var loadout = new PlayerLoadout(new List<ItemData> { armor });

            loadout.Equip(armor);

            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.RightHand));
            Assert.AreSame(armor, loadout.GetEquipped(EquipmentSlotType.Head));
        }

        [Test]
        public void RestoreFrom_OldSaveWeaponInHead_MovesToFirstFreeHand()
        {
            var sword = MakeItem(ItemCategory.Weapon, EquipmentSlotType.Head);
            var data = new SaveData();
            data.itemPool.Add(ItemSnapshot.Capture(sword));
            data.inventory.Add(0);
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.Head, itemIndex = 0 });

            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            Assert.IsNull(restored.GetEquipped(EquipmentSlotType.Head));
            var equippedSword = restored.GetEquipped(EquipmentSlotType.LeftHand);
            Assert.IsNotNull(equippedSword);
            Assert.AreEqual(sword.title, equippedSword.title);
            Assert.AreEqual(EquipmentSlotType.LeftHand, equippedSword.slotType);
        }

        [Test]
        public void RestoreFrom_OldSaveMisplacedWeapon_NoFreeHand_StaysUnequipped()
        {
            var handA = MakeItem(ItemCategory.Weapon, EquipmentSlotType.LeftHand, "HandA");
            var handB = MakeItem(ItemCategory.Weapon, EquipmentSlotType.RightHand, "HandB");
            var stray = MakeItem(ItemCategory.Weapon, EquipmentSlotType.Head, "Stray");
            var data = new SaveData();
            data.itemPool.Add(ItemSnapshot.Capture(handA));
            data.itemPool.Add(ItemSnapshot.Capture(handB));
            data.itemPool.Add(ItemSnapshot.Capture(stray));
            data.inventory.AddRange(new[] { 0, 1, 2 });
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.LeftHand, itemIndex = 0 });
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.RightHand, itemIndex = 1 });
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.Head, itemIndex = 2 });

            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            Assert.IsNull(restored.GetEquipped(EquipmentSlotType.Head));
            Assert.AreEqual("HandA", restored.GetEquipped(EquipmentSlotType.LeftHand).title);
            Assert.AreEqual("HandB", restored.GetEquipped(EquipmentSlotType.RightHand).title);
        }

        [Test]
        public void RestoreFrom_ValidArmorInHead_IsKept()
        {
            var helm = MakeItem(ItemCategory.Armor, EquipmentSlotType.Head);
            var data = new SaveData();
            data.itemPool.Add(ItemSnapshot.Capture(helm));
            data.inventory.Add(0);
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.Head, itemIndex = 0 });

            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            Assert.IsNotNull(restored.GetEquipped(EquipmentSlotType.Head));
            Assert.AreEqual(EquipmentSlotType.Head, restored.GetEquipped(EquipmentSlotType.Head).slotType);
        }

        [Test]
        public void RestoreFrom_ValidHandSlot_IsKept()
        {
            var sword = MakeItem(ItemCategory.Weapon, EquipmentSlotType.RightHand);
            var data = new SaveData();
            data.itemPool.Add(ItemSnapshot.Capture(sword));
            data.inventory.Add(0);
            data.equipped.Add(new EquippedEntry { slot = EquipmentSlotType.RightHand, itemIndex = 0 });

            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            Assert.IsNotNull(restored.GetEquipped(EquipmentSlotType.RightHand));
            Assert.IsNull(restored.GetEquipped(EquipmentSlotType.LeftHand));
        }

        [Test]
        public void EquipThenSaveAndLoad_StaleWeapon_KeepsItsHand()
        {
            var sword = MakeItem(ItemCategory.Weapon, EquipmentSlotType.Head);
            var loadout = new PlayerLoadout(new List<ItemData> { sword });
            loadout.Equip(sword);

            var data = new SaveData();
            loadout.CaptureInto(data);
            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            Assert.IsNull(restored.GetEquipped(EquipmentSlotType.Head));
            Assert.IsNotNull(restored.GetEquipped(EquipmentSlotType.LeftHand));
        }

        private static ItemData MakeItem(ItemCategory category, EquipmentSlotType slot, string title = null)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.category = category;
            item.slotType = slot;
            item.title = title ?? $"Test {category}";
            return item;
        }
    }
}
