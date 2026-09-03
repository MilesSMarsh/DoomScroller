using System;
using System.Collections.Generic;

namespace DoomScroller.Progression
{
    // Plain C# so allocation math is testable without a running scene, same convention
    // as PlayerProgression/PlayerHealth. Points are spent immediately on tap — no
    // "-"/undo, no staging/confirm step, matching this project's existing immediate-
    // action UI conventions (e.g. tap-to-equip).
    public class PlayerStats
    {
        // Public so ProfileController can calibrate PlayerHealth's starting max around
        // it without duplicating the number.
        public const int BaseValue = 5;

        private readonly Dictionary<PlayerStat, int> _values = new();

        public int UnspentPoints { get; private set; }

        public event Action Changed;

        public PlayerStats()
        {
            foreach (PlayerStat stat in Enum.GetValues(typeof(PlayerStat)))
            {
                _values[stat] = BaseValue;
            }
        }

        public int Get(PlayerStat stat)
        {
            return _values[stat];
        }

        // Called by ProfileController whenever PlayerProgression.LeveledUp fires.
        public void GrantPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            UnspentPoints += amount;
            Changed?.Invoke();
        }

        public void Allocate(PlayerStat stat)
        {
            if (UnspentPoints <= 0)
            {
                return;
            }

            _values[stat]++;
            UnspentPoints--;
            Changed?.Invoke();
        }
    }
}
