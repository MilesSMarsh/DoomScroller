namespace DoomScroller.Inventory
{
    // Ordinal-serialized by Unity — always append new values at the end, never insert,
    // same rule as ItemCategory/ItemRarity/WeaponType. Only meaningful when an ItemData's
    // category is Weapon. See DamageTypeModifier for how an encounter's enemy can be
    // weak/resistant to a given type.
    public enum DamageType
    {
        Slash,
        Blunt,
        Pierce,
        Arcane,
        Elemental
    }
}
