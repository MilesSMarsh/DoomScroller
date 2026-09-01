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
                XpToNextLevel = Mathf.RoundToInt(BaseXpToNextLevel * (1f + (Level - 1) * XpGrowthPerLevel));
                LeveledUp?.Invoke(Level);
            }

            Changed?.Invoke();
        }
    }
}
