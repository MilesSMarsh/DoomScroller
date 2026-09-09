using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so leveling math is testable without a running scene.
    public class PlayerProgression
    {
        private const int BaseXpToNextLevel = 100;
        private const float XpGrowthPerLevel = 0.25f;

        public int Level { get; private set; } = 1;
        public int CurrentXp { get; private set; }
        public int XpToNextLevel { get; private set; } = BaseXpToNextLevel;

        public event Action Changed;
        public event Action<int> LeveledUp;

        public void AddXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentXp += amount;
            while (CurrentXp >= XpToNextLevel)
            {
                CurrentXp -= XpToNextLevel;
                Level++;
                XpToNextLevel = ComputeXpToNextLevel(Level);
                LeveledUp?.Invoke(Level);
            }

            Changed?.Invoke();
        }

        private static int ComputeXpToNextLevel(int level)
        {
            return Mathf.RoundToInt(BaseXpToNextLevel * (1f + (level - 1) * XpGrowthPerLevel));
        }

        // The save/load path — restores Level/CurrentXp directly and recomputes
        // XpToNextLevel via the same formula AddXp uses, so the two can never drift.
        // Doesn't fire Changed — called once, before anything subscribes.
        public void LoadState(int level, int currentXp)
        {
            Level = level;
            CurrentXp = currentXp;
            XpToNextLevel = ComputeXpToNextLevel(level);
        }
    }
}
