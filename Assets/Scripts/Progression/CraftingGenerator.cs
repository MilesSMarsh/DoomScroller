using System;
using System.Collections.Generic;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // One item craftable at the Blacksmith, priced in gold + crafting materials — see
    // CraftingGenerator.GenerateRecipes. Materials cost is undifferentiated ("any N
    // materials, regardless of type") — real crafting trees with named-material recipes,
    // gated by which materials the player has actually seen, are a later iteration (see
    // the crafting_recipe_unlock_design project memory) — not implemented yet, so every
    // hand-authored item in a category is unconditionally available today.
    public readonly struct CraftingRecipe
    {
        public ItemData Item { get; }
        public int GoldCost { get; }
        public int MaterialsCost { get; }

        public CraftingRecipe(ItemData item, int goldCost, int materialsCost)
        {
            Item = item;
            GoldCost = goldCost;
            MaterialsCost = materialsCost;
        }
    }

    // Every hand-authored ItemData of a given category, from Assets/Resources/Items —
    // a fixed catalog, not a random roll (that's what VillageBlacksmithView used to get
    // from here; see this session's note above for why it changed). Flat, placeholder
    // pricing derived from each item's own goldValue — "balance later," same as goldValue
    // itself.
    public static class CraftingGenerator
    {
        private const string ItemsResourcesPath = "Items";
        private const float GoldCostMultiplier = 2f;
        private const int MaterialsCostFlat = 3;

        public static IReadOnlyList<CraftingRecipe> GenerateRecipes(ItemCategory category)
        {
            var items = Resources.LoadAll<ItemData>(ItemsResourcesPath);
            Array.Sort(items, (a, b) => string.CompareOrdinal(a.title, b.title));

            var recipes = new List<CraftingRecipe>();
            foreach (var item in items)
            {
                if (item.category != category)
                {
                    continue;
                }

                var goldCost = Mathf.Max(1, Mathf.RoundToInt(item.goldValue * GoldCostMultiplier));
                recipes.Add(new CraftingRecipe(item, goldCost, MaterialsCostFlat));
            }

            return recipes;
        }
    }
}
