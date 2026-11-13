using System;

namespace DoomScroller.CombatCore
{
    public enum CombatActor
    {
        Player,
        Enemy
    }

    // One resolved swing/cast from the simulation — the playback view replays these in
    // order to animate the fight. DisplayDamage is the real damage that landed (0 and
    // meaningless when WasMiss is true), not a cosmetic number. Public fields (not
    // properties) so JsonUtility can carry a FightResult's beat list over the network.
    [Serializable]
    public struct CombatBeat
    {
        public CombatActor Actor;

        // True for every ability cast, and for a basic attack from a ranged weapon —
        // drives whether the playback spawns a projectile or plays a melee lunge.
        public bool IsRanged;

        public bool WasMiss;
        public int DisplayDamage;

        // Null/empty for a basic attack.
        public string AbilityName;

        // A damage-over-time tick — always AbilityName == null (so it never collides
        // with a real ability's cooldown-bar widget) but the playback still needs to
        // tell it apart from a plain basic-attack beat.
        public bool IsDotTick;

        public CombatBeat(CombatActor actor, bool isRanged, bool wasMiss, int displayDamage, string abilityName = null,
            bool isDotTick = false)
        {
            Actor = actor;
            IsRanged = isRanged;
            WasMiss = wasMiss;
            DisplayDamage = displayDamage;
            AbilityName = abilityName;
            IsDotTick = isDotTick;
        }
    }
}
