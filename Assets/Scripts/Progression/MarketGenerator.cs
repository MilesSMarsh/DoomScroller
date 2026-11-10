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
        private const string MaterialDefinitionsResourcesPath = "MaterialDefinitions";
        private const int StockCount = 5;
        private static readonly int[] TierPrices = { 20, 45, 90 };

        private const float MaterialChance = 0.3f;
        private const int MaterialPrice = 15;

        // Potions aren't sold here — that's the Tavern's own thing (refills, not
        // purchases). Weapons ARE now in the rotation (per the user's own call), rolled
        // from the authored WeaponDefinition catalog (see GenerateGear) rather than
        // RewardGenerator.GenerateItem's own generic-archetype Weapon handling.
        private static readonly ItemCategory[] GearCategories =
        {
            ItemCategory.Armor, ItemCategory.Weapon, ItemCategory.Shield, ItemCategory.Talisman
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
            var rarity = (ItemRarity)tier;
            var category = GearCategories[rng.Next(GearCategories.Length)];
            var item = category == ItemCategory.Weapon
                ? WeaponGenerator.GenerateFromRandomDefinition(rarity, rng)
                : RewardGenerator.GenerateItem(rarity, rng, new[] { category });
            return new MarketEntry(item, TierPrices[tier]);
        }

        // Falls back to a gear slot on the (should-never-happen) chance the
        // MaterialDefinitions catalog is empty — losing one stock slot's variety beats
        // an unauthored/emptied catalog silently taking down the rest of VillageView.Show()
        // (materials are called before Blacksmith/the page-width fix-up, so an uncaught
        // exception here used to abort those too — see GenerateMaterialItem).
        private static MarketEntry GenerateMaterial(System.Random rng)
        {
            var item = GenerateMaterialItem(rng);
            return item != null ? new MarketEntry(item, MaterialPrice) : GenerateGear(rng);
        }

        // Rolls a random authored MaterialDefinition and generates a fresh Plain-rarity
        // instance from it (MaterialGenerator.GenerateFromDefinition) — materials are
        // generated instances now, same as weapons/equipment, not shared singleton
        // ItemData assets (see MaterialDefinition's own doc comment). Null if the
        // catalog is empty.
        public static ItemData GenerateMaterialItem(System.Random rng)
        {
            var definitions = Resources.LoadAll<MaterialDefinition>(MaterialDefinitionsResourcesPath);
            if (definitions.Length == 0)
            {
                return null;
            }

            var definition = definitions[rng.Next(definitions.Length)];
            return MaterialGenerator.GenerateFromDefinition(definition, ItemRarity.Plain);
        }
    }
}
