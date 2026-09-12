using System.Collections.Generic;
using System.Linq;
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
        private const string ItemsResourcesPath = "Items";
        private const int StockCount = 5;
        private static readonly int[] TierPrices = { 20, 45, 90 };

        private const float MaterialChance = 0.3f;
        private const int MaterialPrice = 15;

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
            var item = RewardGenerator.GenerateItem((ItemRarity)tier, rng, GearCategories);
            return new MarketEntry(item, TierPrices[tier]);
        }

        private static MarketEntry GenerateMaterial(System.Random rng)
        {
            return new MarketEntry(GenerateMaterialItem(rng), MaterialPrice);
        }

        // Returns one of the singleton hand-authored Material ItemData assets (Iron Ore,
        // Wood, Stone, ...) rather than a freshly-synthesized instance — this used to
        // invent its own placeholder names/instances since materials cost was
        // undifferentiated ("any N materials"), but RecipeDefinition ingredients now name
        // a *specific* material asset, so what the Market hands out has to be the same
        // reference a recipe can actually check for (see PlayerLoadout.HasDiscovered/
        // ConsumeNamedIngredients, which both compare by reference).
        public static ItemData GenerateMaterialItem(System.Random rng)
        {
            var materials = Resources.LoadAll<ItemData>(ItemsResourcesPath)
                .Where(i => i.category == ItemCategory.Material)
                .ToArray();
            return materials[rng.Next(materials.Length)];
        }
    }
}
