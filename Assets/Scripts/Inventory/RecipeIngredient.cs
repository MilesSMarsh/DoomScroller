using System;

namespace DoomScroller.Inventory
{
    // One named-material line in a RecipeDefinition's ingredient list — replaces the old
    // undifferentiated "any N materials" cost with "N of this specific material."
    [Serializable]
    public class RecipeIngredient
    {
        public ItemData material; // expected category == Material
        public int count = 1;
    }
}
