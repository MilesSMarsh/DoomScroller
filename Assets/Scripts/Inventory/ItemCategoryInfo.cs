namespace DoomScroller.Inventory
{
    // Single source of truth for category -> label/slot mapping, so the UI and
    // RewardGenerator can't drift out of sync with each other.
    public static class ItemCategoryInfo
    {
        public static string Label(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Weapon => "Damage",
                ItemCategory.Armor => "Defence",
                ItemCategory.Shield => "Defence",
                ItemCategory.Talisman => "Power",
                _ => "Power"
            };
        }

        public static EquipmentSlotType[] SlotsFor(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Armor => new[]
                {
                    EquipmentSlotType.Head, EquipmentSlotType.Chest,
                    EquipmentSlotType.Legs, EquipmentSlotType.Boots
                },
                ItemCategory.Weapon => new[] { EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand },
                ItemCategory.Shield => new[] { EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand },
                ItemCategory.Talisman => new[] { EquipmentSlotType.Talisman1, EquipmentSlotType.Talisman2 },
                _ => System.Array.Empty<EquipmentSlotType>()
            };
        }

        // Shared by EquipSlotView and EquipSlotDetailView so the display name for a slot
        // only lives in one place.
        public static string SlotLabel(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.LeftHand => "Left Hand",
                EquipmentSlotType.RightHand => "Right Hand",
                EquipmentSlotType.Talisman1 => "Talisman 1",
                EquipmentSlotType.Talisman2 => "Talisman 2",
                _ => slot.ToString()
            };
        }
    }
}
