using System;

namespace DoomScroller.Inventory
{
    // One named-material line in a RecipeDefinition's ingredient list — "N of this
    // specific material family." Which RARITY tier is required is never authored here —
    // it's resolved at craft time from context (the recipe's own output rarity, or a
    // weapon's current rarity for Empower/Reroll — see PlayerLoadout's material-counting
    // methods), matching every other material family the same way.
    [Serializable]
    public class RecipeIngredient
    {
        public MaterialDefinition material;
        public int count = 1;
    }
}
