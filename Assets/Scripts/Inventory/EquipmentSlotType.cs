namespace DoomScroller.Inventory
{
    // Ordinal values matter: Unity serializes enum fields as plain ints, so existing
    // assets' stored slotType would silently reinterpret as the wrong slot if a member
    // were inserted in the middle. New slots must only ever be appended at the end.
    public enum EquipmentSlotType
    {
        Head,
        Chest,
        Legs,
        Boots,
        LeftHand,
        RightHand,
        Talisman1,
        Talisman2
    }
}
