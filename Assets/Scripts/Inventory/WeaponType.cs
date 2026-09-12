namespace DoomScroller.Inventory
{
    // Ordinal-serialized by Unity — always append new values at the end, never insert,
    // same rule as ItemCategory/ItemRarity. Only meaningful when an ItemData's category
    // is Weapon. Gates which AbilityDefinition/PassiveDefinition catalog entries can roll
    // on a given weapon archetype (see WeaponGenerator.Generate) — a hand-authored
    // WeaponDefinition instead curates its own specific pool (see
    // WeaponGenerator.GenerateFromDefinition), independent of this gating.
    public enum WeaponType
    {
        Melee,
        Ranged,
        Magic
    }
}
