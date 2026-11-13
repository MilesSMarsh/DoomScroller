namespace DoomScroller.CombatCore
{
    // Which stat this effect touches, and which side it touches, is fixed per type —
    // there's no legal combination where e.g. ArmorUp would target the enemy, so a
    // free-form target field would just be a footgun. See CombatSimulator for the math.
    // Ordinal-serialized — append new values at the end only.
    public enum StatusEffectType
    {
        DamageOverTime,   // enemy-only: flat damage per tick
        WeakenDamage,     // enemy-only: % reduction to the enemy's outgoing damage
        SlowAttackSpeed,  // enemy-only: % increase to the enemy's attack interval
        HasteAttackSpeed, // self-only: % decrease to the player's attack interval
        EmpowerDamage,    // self-only: % increase to the player's outgoing damage
        ArmorUp           // self-only: flat defense bonus
    }
}
