using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
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

        [Test]
        public void GetInventory_HandSlot_ReturnsEveryWeapon_RegardlessOfItsOwnSlotType()
        {
            // Neither hand is pinned any more — browsing either one shows the full
            // weapon collection, not just whichever half happened to roll that hand.
            var leftPinned = TestFixtures.MakeWeapon(10, 1f, slot: EquipmentSlotType.LeftHand);
            var rightPinned = TestFixtures.MakeWeapon(10, 1f, slot: EquipmentSlotType.RightHand);
            var loadout = new PlayerLoadout(new List<ItemData> { leftPinned, rightPinned });

            var leftOptions = loadout.GetInventory(EquipmentSlotType.LeftHand);
            var rightOptions = loadout.GetInventory(EquipmentSlotType.RightHand);

            CollectionAssert.AreEquivalent(new[] { leftPinned, rightPinned }, leftOptions);
            CollectionAssert.AreEquivalent(new[] { leftPinned, rightPinned }, rightOptions);
        }

        [Test]
        public void Equip_ExplicitTargetSlot_PlacesItemThere_RegardlessOfItsOwnSlotType()
        {
            var weapon = TestFixtures.MakeWeapon(10, 1f, slot: EquipmentSlotType.LeftHand);
            var loadout = new PlayerLoadout(new List<ItemData> { weapon });

            loadout.Equip(weapon, EquipmentSlotType.RightHand);

            Assert.AreSame(weapon, loadout.GetEquipped(EquipmentSlotType.RightHand));
            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.LeftHand));
        }

        [Test]
        public void Equip_ItemWornInOtherHand_MovesItInsteadOfDuplicatingIt()
        {
            var weapon = TestFixtures.MakeWeapon(10, 1f);
            var loadout = new PlayerLoadout(new List<ItemData> { weapon });
            loadout.Equip(weapon, EquipmentSlotType.LeftHand);

            loadout.Equip(weapon, EquipmentSlotType.RightHand);

            Assert.AreSame(weapon, loadout.GetEquipped(EquipmentSlotType.RightHand));
            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.LeftHand));
        }

        [Test]
        public void Equip_TwoHandedWeapon_LocksOtherHand_RegardlessOfWhichHandItWasEquippedInto()
        {
            var greatsword = TestFixtures.MakeWeapon(20, 0.8f, isTwoHanded: true);
            var loadout = new PlayerLoadout(new List<ItemData> { greatsword });

            loadout.Equip(greatsword, EquipmentSlotType.RightHand);

            Assert.AreSame(greatsword, loadout.GetEquipped(EquipmentSlotType.RightHand));
            Assert.AreSame(greatsword, loadout.GetLockingTwoHandedWeapon(EquipmentSlotType.LeftHand));
        }

        [Test]
        public void HasIngredients_OnlyCountsMaterialsAtTheRequestedRarity()
        {
            var wood = ScriptableObject.CreateInstance<MaterialDefinition>();
            wood.materialName = "Wood";
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Common),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Rare)
            });

            var ingredient = new RecipeIngredient { material = wood, count = 2 };

            // Only 1 unit at each rarity is owned — 2 Common Wood isn't satisfied by
            // owning 1 Common + 1 Rare, and vice versa. Stacks never merge across rarity.
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Common));
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Rare));
        }

        [Test]
        public void HasIngredients_SucceedsOnceEnoughOfTheExactRarityIsOwned()
        {
            var wood = ScriptableObject.CreateInstance<MaterialDefinition>();
            wood.materialName = "Wood";
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Common),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Common),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Rare)
            });

            var ingredient = new RecipeIngredient { material = wood, count = 2 };

            Assert.IsTrue(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Common));
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Rare));
        }

        [Test]
        public void HasDiscovered_IsKeyedByFamilyAndRarity_NotFamilyAlone()
        {
            var wood = ScriptableObject.CreateInstance<MaterialDefinition>();
            wood.materialName = "Wood";
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Common)
            });

            Assert.IsTrue(loadout.HasDiscovered(wood, ItemRarity.Common));
            Assert.IsFalse(loadout.HasDiscovered(wood, ItemRarity.Rare));
        }

        [Test]
        public void TryCraftRecipe_MaterialOutput_ConsumesIngredientsAtTheRecipesOwnOutputRarity()
        {
            var fibers = ScriptableObject.CreateInstance<MaterialDefinition>();
            fibers.materialName = "Fibers";
            var rope = ScriptableObject.CreateInstance<MaterialDefinition>();
            rope.materialName = "Rope";

            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.recipeId = "test-rope";
            recipe.outputKind = RecipeOutputKind.Material;
            recipe.outputMaterial = rope;
            recipe.outputRarity = ItemRarity.Rare;
            recipe.goldCost = 5;
            recipe.ingredients = new[] { new RecipeIngredient { material = fibers, count = 2 } };

            var rareFibersA = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Rare);
            var rareFibersB = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Rare);
            var commonFibers = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Common);
            var loadout = new PlayerLoadout(new List<ItemData> { rareFibersA, rareFibersB, commonFibers });
            var wallet = new PlayerWallet(5);

            var crafted = loadout.TryCraftRecipe(recipe, wallet, 5);

            Assert.IsTrue(crafted);
            // Both Rare Fibers consumed; the Common one is untouched — rarity, not just
            // family, determines what a recipe actually spends.
            Assert.IsFalse(loadout.GetAllInventory().Contains(rareFibersA));
            Assert.IsFalse(loadout.GetAllInventory().Contains(rareFibersB));
            Assert.IsTrue(loadout.GetAllInventory().Contains(commonFibers));
            Assert.IsTrue(loadout.GetAllInventory().Any(i => i.materialSource == rope && i.rarity == ItemRarity.Rare));
        }
    }
}
