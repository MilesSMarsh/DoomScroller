using UnityEngine;

namespace DoomScroller.Inventory
{
    // Single source of truth for rarity -> color, so the item-icon border
    // (GearRowFactory.SetRarityBorder) and any reward-roll fallback tint can't drift out
    // of sync with each other. Grey/Green/Blue/Purple/Gold for Common through Mythic —
    // the user's own explicit call on this ordering.
    public static class ItemRarityInfo
    {
        private static readonly Color[] Colors =
        {
            new(0.7f, 0.7f, 0.7f, 1f), // Common — Grey
            new(0.3f, 0.75f, 0.3f, 1f), // Uncommon — Green
            new(0.25f, 0.55f, 0.9f, 1f), // Rare — Blue
            new(0.6f, 0.35f, 0.85f, 1f), // Legendary — Purple
            new(0.95f, 0.8f, 0.25f, 1f) // Mythic — Gold
        };

        public static Color ColorFor(ItemRarity rarity) => Colors[(int)rarity];
    }
}
