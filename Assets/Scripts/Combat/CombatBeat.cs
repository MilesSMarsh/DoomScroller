namespace DoomScroller.Combat
{
    public enum CombatActor
    {
        Player,
        Enemy
    }

    // One resolved swing/cast from the simulation in CombatResolver — CombatPlaybackView
    // replays these in order to animate the fight. DisplayDamage is the real damage that
    // landed (0 and meaningless when WasMiss is true), not a cosmetic number.
    public readonly struct CombatBeat
    {
        public CombatActor Actor { get; }

        // True for every ability cast, and for a basic attack from a ranged weapon
        // (ItemData.isRanged) — drives whether CombatPlaybackView spawns a projectile
        // or plays a melee lunge.
        public bool IsRanged { get; }

        public bool WasMiss { get; }
        public int DisplayDamage { get; }

        // Null/empty for a basic attack.
        public string AbilityName { get; }

        public CombatBeat(CombatActor actor, bool isRanged, bool wasMiss, int displayDamage, string abilityName = null)
        {
            Actor = actor;
            IsRanged = isRanged;
            WasMiss = wasMiss;
            DisplayDamage = displayDamage;
            AbilityName = abilityName;
        }
    }
}
