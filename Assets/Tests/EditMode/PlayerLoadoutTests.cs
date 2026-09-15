using System.Linq;
using DoomScroller.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class PlayerLoadoutTests
    {
        private static ItemData MakeItem(ItemCategory category, EquipmentSlotType slotType)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.category = category;
            item.slotType = slotType;
            return item;
        }

        [Test]
        public void GetInventory_ExcludesMaterials_EvenThoughTheirUnsetSlotTypeDefaultsToHead()
        {
            // Material never has a meaningful slotType authored — it defaults to the
            // enum's ordinal 0 (Head), which used to leak every owned Material into the
            // Head slot's equip picker.
            var material = MakeItem(ItemCategory.Material, EquipmentSlotType.Head);
            var helm = MakeItem(ItemCategory.Armor, EquipmentSlotType.Head);
            var loadout = TestFixtures.MakeLoadout(material, helm);

            var options = loadout.GetInventory(EquipmentSlotType.Head);

            CollectionAssert.DoesNotContain(options, material);
            CollectionAssert.Contains(options, helm);
        }

        [Test]
        public void GetInventory_ExcludesPotions()
        {
            var potion = MakeItem(ItemCategory.Potion, EquipmentSlotType.Head);
            var helm = MakeItem(ItemCategory.Armor, EquipmentSlotType.Head);
            var loadout = TestFixtures.MakeLoadout(potion, helm);

            var options = loadout.GetInventory(EquipmentSlotType.Head);

            CollectionAssert.DoesNotContain(options, potion);
            CollectionAssert.Contains(options, helm);
        }

        [Test]
        public void GetInventory_OnlyReturnsItemsMatchingTheExactRequestedSlot()
        {
            var helm = MakeItem(ItemCategory.Armor, EquipmentSlotType.Head);
            var boots = MakeItem(ItemCategory.Armor, EquipmentSlotType.Boots);
            var loadout = TestFixtures.MakeLoadout(helm, boots);

            var headOptions = loadout.GetInventory(EquipmentSlotType.Head);

            Assert.AreEqual(1, headOptions.Count);
            Assert.AreSame(helm, headOptions[0]);
        }
    }
}
