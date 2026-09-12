using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // One page of a dungeon run — an enemy fight at increasing depth, an optional rest
    // stop, or the guaranteed treasure room that always caps the run. Generated once per
    // run by DungeonGenerator; DungeonView pages between these, resolving each the first
    // time its page is reached and just re-displaying the stored result on revisits.
    public readonly struct DungeonStep
    {
        // The step's position in the generated sequence (_steps/_pages index) — always a
        // clean 0..N-1 run regardless of whether a rest step is present. Distinct from
        // RewardTierIndex below, which only fight steps use.
        public int DepthIndex { get; }
        public string Label { get; }
        public string RewardSummary { get; }
        public int XpReward { get; }
        public bool IsTreasureRoom { get; }
        public bool IsRestRoom { get; }

        // Which reward rarity (RewardGenerator.GenerateItem) a fight step grants —
        // separate from DepthIndex so inserting a rest step can shift a later fight's
        // position without changing its intended rarity. Meaningless for rest/treasure
        // steps.
        public ItemRarity RewardRarity { get; }

        // Enemy combat stats for the Phase 8 simulation — meaningless when IsTreasureRoom
        // or IsRestRoom is true (both resolve instantly with a null CombatSimulator).
        public int EnemyHealth { get; }
        public int EnemyAttackDamage { get; }
        public float EnemyAttackSpeed { get; }

        // Carried straight through from the dungeon's own EncounterData — the same
        // weaknesses apply at every depth. Meaningless for rest/treasure steps.
        public DamageTypeModifier[] EnemyWeaknesses { get; }

        // Placeholder "art" for this page — a flat color distinct per depth, greybox
        // convention, same as every other fallback-color visual in this project.
        public Color StepColor { get; }

        public DungeonStep(int depthIndex, string label, string rewardSummary, int xpReward, ItemRarity rewardRarity,
            int enemyHealth, int enemyAttackDamage, float enemyAttackSpeed, DamageTypeModifier[] enemyWeaknesses,
            bool isTreasureRoom, bool isRestRoom, Color stepColor)
        {
            DepthIndex = depthIndex;
            Label = label;
            RewardSummary = rewardSummary;
            XpReward = xpReward;
            RewardRarity = rewardRarity;
            EnemyHealth = enemyHealth;
            EnemyAttackDamage = enemyAttackDamage;
            EnemyAttackSpeed = enemyAttackSpeed;
            EnemyWeaknesses = enemyWeaknesses;
            IsTreasureRoom = isTreasureRoom;
            IsRestRoom = isRestRoom;
            StepColor = stepColor;
        }
    }
}
