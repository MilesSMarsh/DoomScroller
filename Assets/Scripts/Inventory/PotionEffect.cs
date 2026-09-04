namespace DoomScroller.Inventory
{
    public enum PotionEffect
    {
        // Restores ItemData.potionAmount to PlayerHealth.CurrentMaxHealth, capped at
        // TrueMaxHealth. Consumed instantly from the Potions tab.
        MaxHealthRestore,

        // Always available, not consumed up front — if the player would die, the
        // strongest owned one (PlayerLoadout.GetBestRevivePotion) is spent to revive
        // them at ItemData.potionAmount HP so the same fight can continue, at most
        // once per fight. See CombatPlaybackView.
        Revive
    }
}
