namespace DoomScroller.Inventory
{
    // Ordinal values matter: Unity serializes enum fields as plain ints, so existing
    // assets' stored category would silently reinterpret as the wrong category if a
    // member were inserted in the middle. New categories must only ever be appended.
    public enum ItemCategory
    {
        Armor,
        Weapon,
        Shield,
        Talisman,
        Potion,

        // Owned, never equipped — same shape as Potion. Feeds the Blacksmith's future
        // crafting stub; for now only ever obtained from the Market.
        Material
    }
}
