using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Every hand-authored RecipeDefinition targeting a given category, read straight from
    // Assets/Resources/RecipeDefinitions — replaces the old CraftingGenerator, which
    // synthesized an undifferentiated "any N materials" recipe from every ItemData in a
    // category. Recipes are now named assets (specific ingredients + prerequisites),
    // authored via the Equipment Menu window's Recipes section
    // (Assets/Editor/EquipmentMenuWindow.cs).
    public static class RecipeCatalog
    {
        private const string RecipeDefinitionsResourcesPath = "RecipeDefinitions";

        public static IReadOnlyList<RecipeDefinition> RecipesFor(ItemCategory category)
        {
            return Resources.LoadAll<RecipeDefinition>(RecipeDefinitionsResourcesPath)
                .Where(r => r.OutputCategory == category)
                .OrderBy(r => r.OutputTitle)
                .ToList();
        }
    }
}
