using System.Collections.Generic;
using System.Linq;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Turns a variant's stated reward tier into an actual, immediately-equippable ItemData —
    // so what the encounter-detail screen promised is exactly what gets granted.
    public static class RewardGenerator
    {
        private static readonly string[] RarityNames = { "Plain", "Tempered", "Runed", "Relic", "Exalted" };

        // Shared across every category — WeaponGenerator reuses this same table for a
        // rolled weapon's base damage (the user's own call: base damage comes from
        // rarity alone, not weapon type).
        public static readonly int[] RarityPower = { 8, 14, 22, 34, 50 };

        // Sell price at the Market's Sell tab — a placeholder scale parallel to
        // RarityPower, tuned later per the user's own call on this. Internal (not
        // private) so WeaponGenerator/WeaponUpgrader can price weapons off the same
        // table instead of leaving item.goldValue at ItemData's bare default.
        internal static readonly int[] RarityGoldValue = { 12, 20, 35, 55, 85 };

        // Gold granted alongside XP for a combat win — one source of truth so
        // CombatResolutionView and DungeonStepPageView can't drift out of sync on how
        // gold scales. No separately-authored field needed; gold just rides the XP
        // reward that's already there.
        private const float GoldPerXpRatio = 0.5f;

        public static int ComputeGoldReward(int xpReward)
        {
            return Mathf.RoundToInt(xpReward * GoldPerXpRatio);
        }

        // EncounterData.goldReward's resolution — 0 (every asset's default before that
        // field existed) falls back to the original XP-derived formula; any authored
        // positive value overrides it outright.
        public static int ResolveGoldReward(int authoredGoldReward, int xpReward)
        {
            return authoredGoldReward > 0 ? authoredGoldReward : ComputeGoldReward(xpReward);
        }

        // EncounterData.lootTablePercentByRarity's roll — normalizes against whatever the
        // table's own total actually is, so an author doesn't have to hit exactly 100.
        // Null/empty/all-zero (every pre-existing encounter, before loot tables existed)
        // always rolls Plain, matching this project's original hardcoded behavior.
        public static ItemRarity RollRarity(float[] percentByRarity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var total = percentByRarity?.Sum() ?? 0f;
            if (total <= 0f)
            {
                return ItemRarity.Plain;
            }

            var roll = rng.NextDouble() * total;
            var cumulative = 0f;
            for (var i = 0; i < percentByRarity.Length; i++)
            {
                cumulative += percentByRarity[i];
                if (roll < cumulative)
                {
                    return (ItemRarity)i;
                }
            }

            return (ItemRarity)(percentByRarity.Length - 1);
        }

        // "+50 XP · 70% Plain, 25% Tempered, 5% Runed Gear" — generated straight from the
        // same table RollRarity itself rolls against, so the feed card's stated odds can
        // never drift from what actually drops. Deliberately never names a specific
        // weapon/item, only rarity (the user's own call) — even when the table has
        // specific weapons/equipment authored into it. Appends an item-count suffix
        // (e.g. "×1-3") when that isn't fixed at exactly 1. Falls back to the original
        // generic default when the table is unset, matching every card authored before
        // loot tables existed.
        public static string DescribeLootTable(LootTable table, int xpReward)
        {
            var percentByRarity = table?.percentByRarity;
            var total = percentByRarity?.Sum() ?? 0f;
            var countSuffix = DescribeItemCountSuffix(table);

            if (total <= 0f)
            {
                return $"+{xpReward} XP · Plain Gear{countSuffix}";
            }

            var parts = new List<string>();
            for (var i = 0; i < percentByRarity.Length; i++)
            {
                if (percentByRarity[i] <= 0f)
                {
                    continue;
                }

                parts.Add($"{Mathf.RoundToInt(percentByRarity[i] / total * 100f)}% {(ItemRarity)i}");
            }

            return $"+{xpReward} XP · {string.Join(", ", parts)} Gear{countSuffix}";
        }

        private static string DescribeItemCountSuffix(LootTable table)
        {
            var min = table?.minItemCount ?? 1;
            var max = table?.maxItemCount ?? 1;
            if (min == 1 && max == 1)
            {
                return "";
            }

            return min == max ? $" ×{min}" : $" ×{min}-{max}";
        }

        // The actual roll — one or more items (LootTable.minItemCount..maxItemCount,
        // inclusive), each independently rolled through the same table: its own rarity
        // roll, then either a specific authored weapon/equipment template at that
        // rarity, or a fully generic archetype roll when neither pool has anything
        // authored (every table's behavior before specific entries existed).
        public static ItemData[] RollLootTable(LootTable table, System.Random rng = null)
        {
            rng ??= new System.Random();
            var min = Mathf.Max(0, Mathf.Min(table?.minItemCount ?? 1, table?.maxItemCount ?? 1));
            var max = Mathf.Max(min, table?.maxItemCount ?? 1);
            var count = rng.Next(min, max + 1);

            var results = new ItemData[count];
            for (var i = 0; i < count; i++)
            {
                results[i] = RollOneItem(table, rng);
            }

            return results;
        }

        private static ItemData RollOneItem(LootTable table, System.Random rng)
        {
            var rarity = RollRarity(table?.percentByRarity, rng);

            var weapons = table?.weapons?.Where(w => w != null).ToList() ?? new List<WeaponDefinition>();
            var equipment = table?.equipment?.Where(e => e != null).ToList() ?? new List<EquipmentDefinition>();
            var materials = table?.materials?.Where(m => m != null).ToList() ?? new List<MaterialDefinition>();
            var total = weapons.Count + equipment.Count + materials.Count;
            if (total == 0)
            {
                // Nothing authored on this table: draw from the authored catalogs, never
                // a "Plain Head"-style generic item.
                return GenerateItem(rarity, rng);
            }

            // One combined draw across all three pools — same "shuffle both pools
            // together" convention as WeaponGenerator.RollSkills, so a weapon, a piece
            // of equipment, and a material are all equally likely candidates, weighted
            // by how many of each are actually authored. All three generate a fresh
            // instance at the same rolled rarity now that materials carry rarity too.
            var pick = rng.Next(total);
            if (pick < weapons.Count)
            {
                return WeaponGenerator.GenerateFromDefinition(weapons[pick], rarity, rng);
            }

            pick -= weapons.Count;
            if (pick < equipment.Count)
            {
                return EquipmentGenerator.GenerateFromDefinition(equipment[pick], rarity, rng);
            }

            return MaterialGenerator.GenerateFromDefinition(materials[pick - equipment.Count], rarity);
        }

        // allowedCategories restricts the roll (the Market uses this to sell only
        // Armor/Shield/Talisman) — null means today's default pool.
        public static ItemData GenerateItem(ItemRarity rarity, System.Random rng = null,
            ItemCategory[] allowedCategories = null)
        {
            rng ??= new System.Random();

            // Potion and Material are excluded from the default pool — a randomly-generated
            // "Plain Potion" doesn't mean anything the way "Plain Head" does (potions are
            // hand-authored content only), and Material has no equip slots at all (would
            // throw below when picking one).
            var categories = allowedCategories ?? ((ItemCategory[])System.Enum.GetValues(typeof(ItemCategory)))
                .Where(c => c != ItemCategory.Potion && c != ItemCategory.Material)
                .ToArray();
            var category = categories[rng.Next(categories.Length)];

            // Prefer authored catalog items; the generic path below only runs when no
            // definition exists for the category.
            if (category == ItemCategory.Weapon)
            {
                return WeaponGenerator.GenerateFromRandomDefinition(rarity, rng);
            }

            var authored = EquipmentGenerator.GenerateFromRandomDefinition(rarity, rng, new[] { category });
            if (authored != null)
            {
                return authored;
            }

            // Weapons get their own richer generation (type, ability/passive rolls,
            // attribute scaling that grows with rarity) — see WeaponGenerator.
            if (category == ItemCategory.Weapon)
            {
                return WeaponGenerator.Generate(rarity, rng);
            }

            var slots = ItemCategoryInfo.SlotsFor(category);
            var slot = slots[rng.Next(slots.Length)];

            var rarityIndex = (int)rarity;
            var rarityName = RarityNames[rarityIndex];
            var power = RarityPower[rarityIndex];

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = $"{rarityName} {slot}";
            item.slotType = slot;
            item.category = category;
            item.rarity = rarity;
            item.statSummary = $"+{power} {ItemCategoryInfo.Label(category)}";
            item.powerValue = power;
            item.itemColor = ItemRarityInfo.ColorFor(rarity);
            item.goldValue = RarityGoldValue[rarityIndex];

            return item;
        }
    }
}
