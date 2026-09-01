using System.Collections.Generic;

namespace DoomScroller.Combat
{
    public readonly struct CombatResult
    {
        public bool IsVictory { get; }
        public int PlayerPower { get; }
        public int ChallengePower { get; }
        public IReadOnlyList<string> SequenceLines { get; }

        public CombatResult(bool isVictory, int playerPower, int challengePower, IReadOnlyList<string> sequenceLines)
        {
            IsVictory = isVictory;
            PlayerPower = playerPower;
            ChallengePower = challengePower;
            SequenceLines = sequenceLines;
        }
    }
}
