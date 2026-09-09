using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Turns a variant's stated reward tier into an actual, immediately-equippable ItemData —
    // so what the encounter-detail screen promised is exactly what gets granted.
    public static class RewardGenerator
    {
        private static readonly string[] RarityNames = { "Common", "Uncommon", "Rare" };
        private static readonly int[] RarityPower = { 8, 14, 22 };

        // Sell price at the Market's Sell tab — a placeholder scale parallel to
        // RarityPower, tuned later per the user's own call on this.
        private static readonly int[] RarityGoldValue = { 12, 20, 35 };
        private static readonly Color[] RarityColors =
        {
            new(0.7f, 0.7f, 0.7f, 1f),
            new(0.3f, 0.6f, 0.9f, 1f),
            new(0.8f, 0.6f, 0.15f, 1f)
        };

        // Gold granted alongside XP for a combat win — one source of truth so
        // CombatResolutionView and DungeonStepPageView can't drift out of sync on how
        // gold scales. No separately-authored field needed; gold just rides the XP
        // reward that's already there.
        private const float GoldPerXpRatio = 0.5f;

        public static int ComputeGoldReward(int xpReward)
        {
            return Mathf.RoundToInt(xpReward * GoldPerXpRatio);
        }

        // allowedCategories restricts the roll (the Market uses this to sell only
        // Armor/Shield/Talisman) — null means today's default pool.
        public static ItemData GenerateItem(int rarityTierIndex, System.Random rng = null,
            ItemCategory[] allowedCategories = null)
        {
            rng ??= new System.Random();
            rarityTierIndex = Mathf.Clamp(rarityTierIndex, 0, RarityNames.Length - 1);

            // Potion and Material are excluded from the default pool — a randomly-generated
            // "Common Potion" doesn't mean anything the way "Common Head" does (potions are
            // hand-authored content only), and Material has no equip slots at all (would
            // throw below when picking one).
            var categories = allowedCategories ?? ((ItemCategory[])System.Enum.GetValues(typeof(ItemCategory)))
                .Where(c => c != ItemCategory.Potion && c != ItemCategory.Material)
                .ToArray();
            var category = categories[rng.Next(categories.Length)];
            var slots = ItemCategoryInfo.SlotsFor(category);
            var slot = slots[rng.Next(slots.Length)];

            var rarityName = RarityNames[rarityTierIndex];
            var power = RarityPower[rarityTierIndex];

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = $"{rarityName} {slot}";
            item.slotType = slot;
            item.category = category;
            item.statSummary = $"+{power} {ItemCategoryInfo.Label(category)}";
            item.powerValue = power;
            item.itemColor = RarityColors[rarityTierIndex];
            item.goldValue = RarityGoldValue[rarityTierIndex];

            if (category == ItemCategory.Weapon)
            {
                item.abilities = new[]
                {
                    new WeaponAbilityData
                    {
                        abilityName = "Strike",
                        description = "A reliable basic attack",
                        powerBonus = power / 2
                    },
                    new WeaponAbilityData
                    {
                        abilityName = "Honed Edge",
                        description = "A quiet, always-on edge to your attacks",
                        powerBonus = power / 4
                    }
                };
            }

            return item;
        }
    }
}
