namespace DoomScroller.Inventory
{
    public enum PotionEffect
    {
        // Restores ItemData.potionAmount to PlayerHealth.CurrentMaxHealth, capped at
        // TrueMaxHealth. Consumed instantly from the Potions tab.
        MaxHealthRestore,

        // Carried into a fight (PlayerLoadout.ToggleRevivePotionActive) rather than
        // consumed up front — if the player would die, it's spent to revive them at
        // ItemData.potionAmount HP so the same fight can continue. See CombatPlaybackView.
        Revive
    }
}
