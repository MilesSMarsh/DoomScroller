using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Turns a variant's stated reward tier into an actual, immediately-equippable ItemData —
    // so what the encounter-detail screen promised is exactly what gets granted.
    public static class RewardGenerator
    {
        private static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Legendary", "Mythic" };

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

        // EncounterData.lootTablePercentByRarity's roll — normalizes against whatever the
        // table's own total actually is, so an author doesn't have to hit exactly 100.
        // Null/empty/all-zero (every pre-existing encounter, before loot tables existed)
        // always rolls Common, matching this project's original hardcoded behavior.
        public static ItemRarity RollRarity(float[] percentByRarity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var total = percentByRarity?.Sum() ?? 0f;
            if (total <= 0f)
            {
                return ItemRarity.Common;
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

        // "+50 XP · 70% Common, 25% Uncommon, 5% Rare Gear" — generated straight from the
        // same table RollRarity itself rolls against, so the feed card's stated odds can
        // never drift from what actually drops. Deliberately never names a specific
        // weapon/item, only rarity (the user's own call). Falls back to the original
        // generic default when the table is unset, matching every card authored before
        // loot tables existed.
        public static string DescribeLootTable(float[] percentByRarity, int xpReward)
        {
            var total = percentByRarity?.Sum() ?? 0f;
            if (total <= 0f)
            {
                return $"+{xpReward} XP · Common Gear";
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

            return $"+{xpReward} XP · {string.Join(", ", parts)} Gear";
        }

        // allowedCategories restricts the roll (the Market uses this to sell only
        // Armor/Shield/Talisman) — null means today's default pool.
        public static ItemData GenerateItem(ItemRarity rarity, System.Random rng = null,
            ItemCategory[] allowedCategories = null)
        {
            rng ??= new System.Random();

            // Potion and Material are excluded from the default pool — a randomly-generated
            // "Common Potion" doesn't mean anything the way "Common Head" does (potions are
            // hand-authored content only), and Material has no equip slots at all (would
            // throw below when picking one).
            var categories = allowedCategories ?? ((ItemCategory[])System.Enum.GetValues(typeof(ItemCategory)))
                .Where(c => c != ItemCategory.Potion && c != ItemCategory.Material)
                .ToArray();
            var category = categories[rng.Next(categories.Length)];

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
