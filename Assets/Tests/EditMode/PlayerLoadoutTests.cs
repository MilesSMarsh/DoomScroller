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
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Plain),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Runed)
            });

            var ingredient = new RecipeIngredient { material = wood, count = 2 };

            // Only 1 unit at each rarity is owned — 2 Plain Wood isn't satisfied by
            // owning 1 Plain + 1 Runed, and vice versa. Stacks never merge across rarity.
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Plain));
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Runed));
        }

        [Test]
        public void HasIngredients_SucceedsOnceEnoughOfTheExactRarityIsOwned()
        {
            var wood = ScriptableObject.CreateInstance<MaterialDefinition>();
            wood.materialName = "Wood";
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Plain),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Plain),
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Runed)
            });

            var ingredient = new RecipeIngredient { material = wood, count = 2 };

            Assert.IsTrue(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Plain));
            Assert.IsFalse(loadout.HasIngredients(new[] { ingredient }, ItemRarity.Runed));
        }

        [Test]
        public void HasDiscovered_IsKeyedByFamilyAndRarity_NotFamilyAlone()
        {
            var wood = ScriptableObject.CreateInstance<MaterialDefinition>();
            wood.materialName = "Wood";
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                MaterialGenerator.GenerateFromDefinition(wood, ItemRarity.Plain)
            });

            Assert.IsTrue(loadout.HasDiscovered(wood, ItemRarity.Plain));
            Assert.IsFalse(loadout.HasDiscovered(wood, ItemRarity.Runed));
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
            recipe.outputRarity = ItemRarity.Runed;
            recipe.goldCost = 5;
            recipe.ingredients = new[] { new RecipeIngredient { material = fibers, count = 2 } };

            var rareFibersA = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Runed);
            var rareFibersB = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Runed);
            var commonFibers = MaterialGenerator.GenerateFromDefinition(fibers, ItemRarity.Plain);
            var loadout = new PlayerLoadout(new List<ItemData> { rareFibersA, rareFibersB, commonFibers });
            var wallet = new PlayerWallet(5);

            var crafted = loadout.TryCraftRecipe(recipe, wallet, 5);

            Assert.IsTrue(crafted);
            // Both Runed Fibers consumed; the Plain one is untouched — rarity, not just
            // family, determines what a recipe actually spends.
            Assert.IsFalse(loadout.GetAllInventory().Contains(rareFibersA));
            Assert.IsFalse(loadout.GetAllInventory().Contains(rareFibersB));
            Assert.IsTrue(loadout.GetAllInventory().Contains(commonFibers));
            Assert.IsTrue(loadout.GetAllInventory().Any(i => i.materialSource == rope && i.rarity == ItemRarity.Runed));
        }

        // ---- Revive percent upgrade (task 007) ----

        private static ItemData MakeRevive(int basePercent = 30)
        {
            var potion = MakeItem(ItemCategory.Potion, EquipmentSlotType.Head);
            potion.potionEffect = PotionEffect.Revive;
            potion.potionAmount = basePercent;
            return potion;
        }

        private static PlayerLoadout LoadoutWithMaterials(ItemData revive, int materials)
        {
            var items = new List<ItemData> { revive };
            for (var i = 0; i < materials; i++)
            {
                items.Add(MakeItem(ItemCategory.Material, EquipmentSlotType.Head));
            }

            return TestFixtures.MakeLoadout(items.ToArray());
        }

        [Test]
        public void RevivePercent_StartsAt30_AndEachLevelAdds10()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 100);
            var wallet = new PlayerWallet(10000);

            Assert.AreEqual(30, loadout.GetRevivePercent(revive));
            for (var level = 1; level <= 4; level++)
            {
                Assert.IsTrue(loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1));
                Assert.AreEqual(30 + level * 10, loadout.GetRevivePercent(revive));
            }
        }

        [Test]
        public void RevivePercent_CapsAt100AfterSevenLevels_AndRefusesFurtherUpgrades()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 100);
            var wallet = new PlayerWallet(10000);

            for (var i = 0; i < PlayerLoadout.MaxUpgradeLevel; i++)
            {
                Assert.IsTrue(loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1));
            }

            Assert.AreEqual(7, PlayerLoadout.MaxUpgradeLevel);
            Assert.AreEqual(100, loadout.GetRevivePercent(revive));

            var goldBefore = wallet.Gold;
            var materialsBefore = loadout.CountMaterials();
            Assert.IsFalse(loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1));
            Assert.AreEqual(100, loadout.GetRevivePercent(revive));
            Assert.AreEqual(goldBefore, wallet.Gold);
            Assert.AreEqual(materialsBefore, loadout.CountMaterials());
        }

        [Test]
        public void ReviveHealAmount_UsesFightStartHp_RoundsUp_NeverZero()
        {
            var revive = MakeRevive();
            var loadout = TestFixtures.MakeLoadout(revive);

            Assert.AreEqual(1, loadout.GetReviveHealAmount(revive, 1));
            Assert.AreEqual(17, loadout.GetReviveHealAmount(revive, 55));
            Assert.AreEqual(30, loadout.GetReviveHealAmount(revive, 100));
            Assert.AreEqual(1, loadout.GetReviveHealAmount(revive, 0));
        }

        [Test]
        public void RefillCapacity_IsThreeAtEveryUpgradeLevel()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 100);
            var wallet = new PlayerWallet(10000);

            for (var level = 0; level <= PlayerLoadout.MaxUpgradeLevel; level++)
            {
                Assert.AreEqual(3, loadout.GetRefillCapacity(revive));
                loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            }
        }

        [Test]
        public void RevivePercentLevel_SurvivesSaveAndLoad()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 10);
            var wallet = new PlayerWallet(1000);
            loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);

            var data = new SaveData();
            loadout.CaptureInto(data);
            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            var restoredRevive = restored.GetKnownRevivePotionType();
            Assert.IsNotNull(restoredRevive);
            Assert.AreEqual(2, restored.GetReviveUpgradeLevel(restoredRevive));
            Assert.AreEqual(50, restored.GetRevivePercent(restoredRevive));
        }

        [Test]
        public void RevivePercent_BaseNearCap_IsClampedAt100_NotOvershot()
        {
            var revive = MakeRevive(basePercent: 95);
            var loadout = new PlayerLoadout(new List<ItemData> { revive, MakeItem(ItemCategory.Material, EquipmentSlotType.Head) });
            var wallet = new PlayerWallet(1000);

            Assert.IsTrue(loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1));

            Assert.AreEqual(100, loadout.GetRevivePercent(revive));
        }

        [Test]
        public void ReviveHealAmount_AtMaxLevel_RestoresFullFightStartHp()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 100);
            var wallet = new PlayerWallet(10000);
            for (var i = 0; i < PlayerLoadout.MaxUpgradeLevel; i++)
            {
                loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            }

            Assert.AreEqual(55, loadout.GetReviveHealAmount(revive, 55));
            Assert.AreEqual(1, loadout.GetReviveHealAmount(revive, 1));
        }

        [Test]
        public void RevivePercentUpgrade_DoesNotMutateSharedPotionAsset()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 10);
            var wallet = new PlayerWallet(1000);

            loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);

            Assert.AreEqual(30, revive.potionAmount);
            Assert.AreEqual(50, loadout.GetRevivePercent(revive));
        }

        [Test]
        public void TryUpgradeRevivePercent_InsufficientGoldOrMaterials_SpendsNothing()
        {
            var revive = MakeRevive();
            var poorLoadout = LoadoutWithMaterials(revive, 5);
            var poorWallet = new PlayerWallet(0);

            Assert.IsFalse(poorLoadout.TryUpgradeRevivePercent(revive, poorWallet, 15, 2));
            Assert.AreEqual(0, poorLoadout.GetReviveUpgradeLevel(revive));
            Assert.AreEqual(5, poorLoadout.CountMaterials());

            var noMaterialsLoadout = LoadoutWithMaterials(revive, 1);
            var richWallet = new PlayerWallet(1000);

            Assert.IsFalse(noMaterialsLoadout.TryUpgradeRevivePercent(revive, richWallet, 15, 2));
            Assert.AreEqual(0, noMaterialsLoadout.GetReviveUpgradeLevel(revive));
            Assert.AreEqual(1000, richWallet.Gold);
            Assert.AreEqual(1, noMaterialsLoadout.CountMaterials());
        }

        [Test]
        public void GetBestRevivePotion_PicksHighestEffectivePercent_IncludingUpgrades()
        {
            var weak = MakeRevive(basePercent: 30);
            var strong = MakeRevive(basePercent: 40);
            var loadout = new PlayerLoadout(new List<ItemData> { weak, strong, MakeItem(ItemCategory.Material, EquipmentSlotType.Head) });
            var wallet = new PlayerWallet(1000);

            Assert.AreSame(strong, loadout.GetBestRevivePotion());

            // Two upgrades on the weak one: 30 + 20 = 50 beats the strong one's flat 40.
            loadout.TryUpgradeRevivePercent(weak, wallet, 1, 1);
            loadout.TryUpgradeRevivePercent(weak, wallet, 1, 1);

            Assert.AreSame(weak, loadout.GetBestRevivePotion());
        }

        [Test]
        public void GetBestRevivePotion_NullWhenNoReviveOwned()
        {
            var loadout = new PlayerLoadout(new List<ItemData> { MakeItem(ItemCategory.Armor, EquipmentSlotType.Head) });

            Assert.IsNull(loadout.GetBestRevivePotion());
        }

        [Test]
        public void TavernRefill_AtMaxUpgrade_TopsRevivesUpToThreeAndNoFurther()
        {
            var revive = MakeRevive();
            var loadout = new PlayerLoadout(new List<ItemData>
            {
                revive,
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head),
                MakeItem(ItemCategory.Material, EquipmentSlotType.Head)
            });
            var wallet = new PlayerWallet(100000);
            for (var i = 0; i < PlayerLoadout.MaxUpgradeLevel; i++)
            {
                loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            }

            // One Revive is already owned (the one in the starting inventory), so a refill to
            // the cap of 3 buys exactly 2.
            var bought = loadout.RefillPotions(wallet, _ => 5, t => t.potionEffect == PotionEffect.Revive);

            Assert.AreEqual(2, bought);
            Assert.AreEqual(3, loadout.GetAllInventory().Count(i => i == revive));
            Assert.IsTrue(loadout.ArePotionsFullyStocked(t => t.potionEffect == PotionEffect.Revive));
            Assert.AreEqual(0, loadout.ComputeRefillCost(_ => 5, t => t.potionEffect == PotionEffect.Revive));
            Assert.AreEqual(0, loadout.RefillPotions(wallet, _ => 5, t => t.potionEffect == PotionEffect.Revive));
        }

        [Test]
        public void RevivePercentLevel_AtMax_SurvivesSaveAndLoad_StillCapsAt100()
        {
            var revive = MakeRevive();
            var loadout = LoadoutWithMaterials(revive, 10);
            var wallet = new PlayerWallet(1000);
            for (var i = 0; i < PlayerLoadout.MaxUpgradeLevel; i++)
            {
                loadout.TryUpgradeRevivePercent(revive, wallet, 1, 1);
            }

            var data = new SaveData();
            loadout.CaptureInto(data);
            var restored = new PlayerLoadout(new ItemData[0]);
            restored.RestoreFrom(data);

            var restoredRevive = restored.GetKnownRevivePotionType();
            Assert.IsNotNull(restoredRevive);
            Assert.AreEqual(PlayerLoadout.MaxUpgradeLevel, restored.GetReviveUpgradeLevel(restoredRevive));
            Assert.AreEqual(100, restored.GetRevivePercent(restoredRevive));
            Assert.IsFalse(restored.TryUpgradeRevivePercent(restoredRevive, new PlayerWallet(1000), 1, 1));
        }
    }
}
