using System.Linq;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class RewardGeneratorTests
    {
        [Test]
        public void RollRarity_NullOrEmptyOrAllZeroTable_AlwaysReturnsCommon()
        {
            var rng = new System.Random(1);
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(null, rng));
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(new float[0], rng));
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(new float[5], rng));
        }

        [Test]
        public void RollRarity_SingleNonZeroRarity_AlwaysWinsRegardlessOfRng()
        {
            var table = new float[5];
            table[(int)ItemRarity.Legendary] = 1f;

            for (var seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(ItemRarity.Legendary, RewardGenerator.RollRarity(table, new System.Random(seed)));
            }
        }

        [Test]
        public void RollRarity_WeightedDistribution_MatchesTableProportions()
        {
            var table = new float[5];
            table[(int)ItemRarity.Common] = 70f;
            table[(int)ItemRarity.Uncommon] = 25f;
            table[(int)ItemRarity.Rare] = 5f;

            var rng = new System.Random(12345);
            const int trials = 20000;
            var counts = new int[5];
            for (var i = 0; i < trials; i++)
            {
                counts[(int)RewardGenerator.RollRarity(table, rng)]++;
            }

            // Generous tolerance — statistical, not exact.
            Assert.AreEqual(0.70f, counts[(int)ItemRarity.Common] / (float)trials, 0.02f);
            Assert.AreEqual(0.25f, counts[(int)ItemRarity.Uncommon] / (float)trials, 0.02f);
            Assert.AreEqual(0.05f, counts[(int)ItemRarity.Rare] / (float)trials, 0.02f);
            Assert.AreEqual(0, counts[(int)ItemRarity.Legendary]);
            Assert.AreEqual(0, counts[(int)ItemRarity.Mythic]);
        }

        [Test]
        public void RollRarity_TableNotSummingToOneHundred_StillNormalizesCorrectly()
        {
            // 2:1 ratio regardless of the total being 30, not 100.
            var table = new float[5];
            table[(int)ItemRarity.Common] = 20f;
            table[(int)ItemRarity.Uncommon] = 10f;

            var rng = new System.Random(999);
            const int trials = 10000;
            var commonCount = 0;
            for (var i = 0; i < trials; i++)
            {
                if (RewardGenerator.RollRarity(table, rng) == ItemRarity.Common)
                {
                    commonCount++;
                }
            }

            Assert.AreEqual(2f / 3f, commonCount / (float)trials, 0.03f);
        }

        private static LootTable MakeTable(params (ItemRarity rarity, float percent)[] entries)
        {
            var table = new LootTable();
            foreach (var (rarity, percent) in entries)
            {
                table.percentByRarity[(int)rarity] = percent;
            }

            return table;
        }

        [Test]
        public void DescribeLootTable_NullOrAllZeroTable_FallsBackToTheOriginalGenericDefault()
        {
            Assert.AreEqual("+50 XP · Common Gear", RewardGenerator.DescribeLootTable(null, 50));
            Assert.AreEqual("+50 XP · Common Gear", RewardGenerator.DescribeLootTable(new LootTable(), 50));
        }

        [Test]
        public void DescribeLootTable_NamesOnlyRarities_NeverASpecificItem()
        {
            var table = MakeTable((ItemRarity.Common, 70f), (ItemRarity.Uncommon, 25f), (ItemRarity.Rare, 5f));

            var text = RewardGenerator.DescribeLootTable(table, 80);

            Assert.AreEqual("+80 XP · 70% Common, 25% Uncommon, 5% Rare Gear", text);
        }

        [Test]
        public void DescribeLootTable_SkipsZeroWeightRarities()
        {
            var table = MakeTable((ItemRarity.Common, 1f), (ItemRarity.Mythic, 1f));

            var text = RewardGenerator.DescribeLootTable(table, 10);

            Assert.AreEqual("+10 XP · 50% Common, 50% Mythic Gear", text);
        }

        [Test]
        public void DescribeLootTable_NormalizesAgainstItsOwnTotal_NotRequiringExactlyOneHundred()
        {
            var table = MakeTable((ItemRarity.Common, 20f), (ItemRarity.Uncommon, 10f));

            var text = RewardGenerator.DescribeLootTable(table, 10);

            Assert.AreEqual("+10 XP · 67% Common, 33% Uncommon Gear", text);
        }

        [Test]
        public void DescribeLootTable_AppendsItemCountSuffix_WhenNotFixedAtOne()
        {
            var fixedTable = MakeTable((ItemRarity.Common, 100f));
            fixedTable.minItemCount = 1;
            fixedTable.maxItemCount = 1;
            Assert.IsFalse(RewardGenerator.DescribeLootTable(fixedTable, 10).Contains("×"), "1-1 shouldn't show a suffix at all");

            var fixedCount = MakeTable((ItemRarity.Common, 100f));
            fixedCount.minItemCount = 3;
            fixedCount.maxItemCount = 3;
            Assert.AreEqual("+10 XP · 100% Common Gear ×3", RewardGenerator.DescribeLootTable(fixedCount, 10));

            var rangeCount = MakeTable((ItemRarity.Common, 100f));
            rangeCount.minItemCount = 1;
            rangeCount.maxItemCount = 3;
            Assert.AreEqual("+10 XP · 100% Common Gear ×1-3", RewardGenerator.DescribeLootTable(rangeCount, 10));
        }

        [Test]
        public void RollLootTable_NullTable_ReturnsExactlyOneGenericItem()
        {
            var items = RewardGenerator.RollLootTable(null, new System.Random(1));
            Assert.AreEqual(1, items.Length);
            Assert.IsNotNull(items[0]);
        }

        [Test]
        public void RollLootTable_ItemCount_AlwaysFallsWithinMinMax()
        {
            var table = MakeTable((ItemRarity.Common, 100f));
            table.minItemCount = 2;
            table.maxItemCount = 4;

            var rng = new System.Random(42);
            for (var i = 0; i < 200; i++)
            {
                var items = RewardGenerator.RollLootTable(table, rng);
                Assert.GreaterOrEqual(items.Length, 2);
                Assert.LessOrEqual(items.Length, 4);
            }
        }

        [Test]
        public void RollLootTable_FixedItemCount_AlwaysReturnsExactlyThatMany()
        {
            var table = MakeTable((ItemRarity.Common, 100f));
            table.minItemCount = 2;
            table.maxItemCount = 2;

            var items = RewardGenerator.RollLootTable(table, new System.Random(1));

            Assert.AreEqual(2, items.Length);
        }

        [Test]
        public void RollLootTable_SpecificWeaponInPool_IsRolledAtTheTablesRarity()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponName = "Test Blade";
            weapon.weaponType = WeaponType.Melee;
            weapon.statsByRarity = new[]
            {
                new WeaponRarityStats { baseDamage = 1, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 2, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 4, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 8, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 16, attributeScalingEntries = System.Array.Empty<AttributeScaling>() }
            };
            weapon.possibleAbilities = System.Array.Empty<AbilityDefinition>();
            weapon.possiblePassives = System.Array.Empty<PassiveDefinition>();

            // Only Rare is possible, and the weapon pool is the only candidate.
            var table = MakeTable((ItemRarity.Rare, 100f));
            table.weapons = new[] { weapon };
            table.minItemCount = 1;
            table.maxItemCount = 1;

            var items = RewardGenerator.RollLootTable(table, new System.Random(1));

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual("Test Blade", items[0].title);
            Assert.AreEqual(ItemRarity.Rare, items[0].rarity);
            Assert.AreEqual(4, items[0].powerValue, "the Rare-tier row's own baseDamage");
        }

        [Test]
        public void RollLootTable_EmptyWeaponAndEquipmentPools_FallsBackToGenericItem()
        {
            var table = MakeTable((ItemRarity.Legendary, 100f));
            table.weapons = System.Array.Empty<WeaponDefinition>();
            table.equipment = System.Array.Empty<EquipmentDefinition>();

            var items = RewardGenerator.RollLootTable(table, new System.Random(1));

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(ItemRarity.Legendary, items[0].rarity);
        }

        [Test]
        public void RollLootTable_CombinedWeaponAndEquipmentPool_DrawsFromBothRoughlyEvenly()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponName = "Pool Weapon";
            weapon.weaponType = WeaponType.Melee;
            weapon.statsByRarity = new[]
            {
                new WeaponRarityStats { baseDamage = 1, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 2, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 4, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 8, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 16, attributeScalingEntries = System.Array.Empty<AttributeScaling>() }
            };
            weapon.possibleAbilities = System.Array.Empty<AbilityDefinition>();
            weapon.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.equipmentName = "Pool Armor";
            equipment.category = ItemCategory.Armor;
            equipment.slotType = EquipmentSlotType.Head;
            equipment.powerByRarity = new[] { 1, 2, 4, 8, 16 };
            equipment.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var table = MakeTable((ItemRarity.Common, 100f));
            table.weapons = new[] { weapon };
            table.equipment = new[] { equipment };
            table.minItemCount = 1;
            table.maxItemCount = 1;

            var rng = new System.Random(7);
            var weaponCount = 0;
            const int trials = 2000;
            for (var i = 0; i < trials; i++)
            {
                if (RewardGenerator.RollLootTable(table, rng)[0].title == "Pool Weapon")
                {
                    weaponCount++;
                }
            }

            Assert.AreEqual(0.5f, weaponCount / (float)trials, 0.05f);
        }

        [Test]
        public void RollLootTable_SpecificMaterialInPool_IsGeneratedAtTheTablesRolledRarity()
        {
            var material = ScriptableObject.CreateInstance<MaterialDefinition>();
            material.materialName = "Iron Ore";

            var table = MakeTable((ItemRarity.Mythic, 100f));
            table.materials = new[] { material };
            table.minItemCount = 1;
            table.maxItemCount = 1;

            var items = RewardGenerator.RollLootTable(table, new System.Random(1));

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual("Iron Ore", items[0].title);
            Assert.AreEqual(ItemCategory.Material, items[0].category);
            Assert.AreEqual(ItemRarity.Mythic, items[0].rarity);
            Assert.AreSame(material, items[0].materialSource);
        }

        [Test]
        public void RollLootTable_CombinedWeaponEquipmentMaterialPool_DrawsFromAllThreeRoughlyEvenly()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponName = "Pool Weapon";
            weapon.weaponType = WeaponType.Melee;
            weapon.statsByRarity = new[]
            {
                new WeaponRarityStats { baseDamage = 1, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 2, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 4, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 8, attributeScalingEntries = System.Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 16, attributeScalingEntries = System.Array.Empty<AttributeScaling>() }
            };
            weapon.possibleAbilities = System.Array.Empty<AbilityDefinition>();
            weapon.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.equipmentName = "Pool Armor";
            equipment.category = ItemCategory.Armor;
            equipment.slotType = EquipmentSlotType.Head;
            equipment.powerByRarity = new[] { 1, 2, 4, 8, 16 };
            equipment.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var material = ScriptableObject.CreateInstance<MaterialDefinition>();
            material.materialName = "Pool Material";

            var table = MakeTable((ItemRarity.Common, 100f));
            table.weapons = new[] { weapon };
            table.equipment = new[] { equipment };
            table.materials = new[] { material };
            table.minItemCount = 1;
            table.maxItemCount = 1;

            var rng = new System.Random(7);
            var materialCount = 0;
            const int trials = 3000;
            for (var i = 0; i < trials; i++)
            {
                if (RewardGenerator.RollLootTable(table, rng)[0].title == "Pool Material")
                {
                    materialCount++;
                }
            }

            Assert.AreEqual(1f / 3f, materialCount / (float)trials, 0.04f);
        }
    }
}
