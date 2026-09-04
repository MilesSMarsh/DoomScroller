using System.Collections.Generic;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // One item for sale at the Market, priced in gold — see MarketGenerator.GenerateStock.
    public readonly struct MarketEntry
    {
        public ItemData Item { get; }
        public int Price { get; }

        public MarketEntry(ItemData item, int price)
        {
            Item = item;
            Price = price;
        }
    }

    // Mirrors RewardGenerator's role one level up (adds a price) — the Village Market's
    // stock, rerolled fresh every visit (no persistent shop inventory to design around
    // yet, per the user's own call on this).
    public static class MarketGenerator
    {
        private const int StockCount = 5;
        private static readonly int[] TierPrices = { 20, 45, 90 };

        private const float MaterialChance = 0.3f;
        private const int MaterialPrice = 15;
        private static readonly string[] MaterialNames =
        {
            "Iron Ore", "Ancient Bone", "Enchanted Thread", "Dragon Scale Fragment"
        };
        private static readonly Color MaterialColor = new(0.6f, 0.5f, 0.35f, 1f);

        // Weapons and Potions aren't sold here — weapons come from combat, potions are
        // the Tavern's own thing (refills, not purchases).
        private static readonly ItemCategory[] GearCategories =
        {
            ItemCategory.Armor, ItemCategory.Shield, ItemCategory.Talisman
        };

        public static IReadOnlyList<MarketEntry> GenerateStock(System.Random rng = null)
        {
            rng ??= new System.Random();
            var entries = new List<MarketEntry>(StockCount);

            for (var i = 0; i < StockCount; i++)
            {
                entries.Add(rng.NextDouble() < MaterialChance ? GenerateMaterial(rng) : GenerateGear(rng));
            }

            return entries;
        }

        private static MarketEntry GenerateGear(System.Random rng)
        {
            var tier = rng.Next(TierPrices.Length);
            var item = RewardGenerator.GenerateItem(tier, rng, GearCategories);
            return new MarketEntry(item, TierPrices[tier]);
        }

        private static MarketEntry GenerateMaterial(System.Random rng)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = MaterialNames[rng.Next(MaterialNames.Length)];
            item.category = ItemCategory.Material;
            item.statSummary = "Crafting material";
            item.powerValue = 0;
            item.itemColor = MaterialColor;
            return new MarketEntry(item, MaterialPrice);
        }
    }
}
