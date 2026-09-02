using System.Collections.Generic;

namespace DoomScroller.Combat
{
    public readonly struct CombatResult
    {
        public bool IsVictory { get; }
        public int PlayerPower { get; }
        public int ChallengePower { get; }
        public IReadOnlyList<string> SequenceLines { get; }

        // Health lost from this encounter: 0 for a clean win or Treasure, a small amount
        // for a defeat, and a smaller "close call" amount for a win that barely cleared
        // the win chance (see CombatResolver's CloseCallMargin).
        public int HealthDamage { get; }
        public bool WasCloseCall { get; }

        public CombatResult(bool isVictory, int playerPower, int challengePower, IReadOnlyList<string> sequenceLines,
            int healthDamage = 0, bool wasCloseCall = false)
        {
            IsVictory = isVictory;
            PlayerPower = playerPower;
            ChallengePower = challengePower;
            SequenceLines = sequenceLines;
            HealthDamage = healthDamage;
            WasCloseCall = wasCloseCall;
        }
    }
}
