using System;

namespace DoomScroller.CombatCore
{
    public enum FightOutcome
    {
        Win,
        Loss
    }

    // The full, ordered record of one resolved fight.
    [Serializable]
    public class FightResult
    {
        public FightOutcome outcome;
        public CombatBeat[] beats = new CombatBeat[0];

        // Whether the armed Revive fired, and the index (into beats) of the beat that
        // killed the player just before it did; -1 when it did not fire.
        public bool reviveFired;
        public int reviveBeatIndex = -1;

        public int finalPlayerHp;
        public int finalEnemyHp;
    }
}
