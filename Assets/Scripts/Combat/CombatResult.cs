using System.Collections.Generic;

namespace DoomScroller.Combat
{
    public readonly struct CombatResult
    {
        public bool IsVictory { get; }
        public IReadOnlyList<CombatBeat> Beats { get; }

        // Total damage actually taken across the whole fight (0 for a flawless win or
        // Treasure) — applied once by the caller via PlayerHealth.ApplyDamage, same as
        // before.
        public int HealthDamage { get; }

        // Victory, but the player ended the fight below CombatResolver.CloseCallHealthFraction
        // of their max health — a post-fight read of how rough it was, not a pre-roll margin.
        public bool WasCloseCall { get; }

        public CombatResult(bool isVictory, IReadOnlyList<CombatBeat> beats, int healthDamage, bool wasCloseCall)
        {
            IsVictory = isVictory;
            Beats = beats;
            HealthDamage = healthDamage;
            WasCloseCall = wasCloseCall;
        }
    }
}
