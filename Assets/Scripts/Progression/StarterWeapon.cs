using DoomScroller.Inventory;

namespace DoomScroller.Progression
{
    // Builds the weapon a new game starts equipped with. Rolled from the authored
    // definition at Plain like any other weapon (never the archetype Generate).
    public static class StarterWeapon
    {
        public static ItemData Create(WeaponDefinition definition, System.Random rng)
        {
            if (definition == null)
            {
                return null;
            }

            return WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Plain, rng);
        }
    }
}
