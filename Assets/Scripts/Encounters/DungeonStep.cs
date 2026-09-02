using UnityEngine;

namespace DoomScroller.Encounters
{
    // One page of a dungeon run — either an enemy fight at increasing depth, or the
    // guaranteed treasure room that always caps the run. Generated once per run by
    // DungeonGenerator; DungeonView pages between these, resolving each the first time
    // its page is reached and just re-displaying the stored result on revisits.
    public readonly struct DungeonStep
    {
        public int DepthIndex { get; }
        public string Label { get; }
        public string RewardSummary { get; }
        public int XpReward { get; }
        public bool IsTreasureRoom { get; }

        // Enemy combat stats for the Phase 8 simulation — meaningless when
        // IsTreasureRoom is true.
        public int EnemyHealth { get; }
        public int EnemyAttackDamage { get; }
        public float EnemyAttackSpeed { get; }

        // Placeholder "art" for this page — a flat color distinct per depth, greybox
        // convention, same as every other fallback-color visual in this project.
        public Color StepColor { get; }

        public DungeonStep(int depthIndex, string label, string rewardSummary, int xpReward,
            int enemyHealth, int enemyAttackDamage, float enemyAttackSpeed, bool isTreasureRoom, Color stepColor)
        {
            DepthIndex = depthIndex;
            Label = label;
            RewardSummary = rewardSummary;
            XpReward = xpReward;
            EnemyHealth = enemyHealth;
            EnemyAttackDamage = enemyAttackDamage;
            EnemyAttackSpeed = enemyAttackSpeed;
            IsTreasureRoom = isTreasureRoom;
            StepColor = stepColor;
        }
    }
}
