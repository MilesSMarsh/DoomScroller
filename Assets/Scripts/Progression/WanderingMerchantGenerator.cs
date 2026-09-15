using System.Collections.Generic;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // The Wandering Merchant feed encounter's stock — mirrors MarketGenerator's role,
    // but deliberately smaller and weapon-inclusive (the static Village Market excludes
    // weapons on purpose; the whole point of a wandering merchant is a rarer, more
    // special find). See WanderingMerchantView for how this gets cached per sighting.
    public static class WanderingMerchantGenerator
    {
        private const int StockCount = 3;

        // Self-authored placeholder, tuned later — markup over an item's own goldValue
        // (which already scales correctly by rarity for both weapons and gear, per
        // WeaponGenerator's own goldValue fix) rather than a separate flat per-tier
        // price table like MarketGenerator's — avoids needing another table to keep in
        // sync as rarities change.
        private const float PriceMarkup = 2.5f;

        private static readonly ItemCategory[] GearCategories =
        {
            ItemCategory.Armor, ItemCategory.Shield, ItemCategory.Talisman
        };

        public static IReadOnlyList<MarketEntry> GenerateStock(System.Random rng = null)
        {
            rng ??= new System.Random();
            var rarities = (ItemRarity[])System.Enum.GetValues(typeof(ItemRarity));
            var entries = new List<MarketEntry>(StockCount);

            for (var i = 0; i < StockCount; i++)
            {
                var rarity = rarities[rng.Next(rarities.Length)];
                var item = rng.NextDouble() < 0.5
                    ? WeaponGenerator.GenerateFromRandomDefinition(rarity, rng)
                    : RewardGenerator.GenerateItem(rarity, rng, GearCategories);
                entries.Add(new MarketEntry(item, Mathf.Max(1, Mathf.RoundToInt(item.goldValue * PriceMarkup))));
            }

            return entries;
        }
    }
}
