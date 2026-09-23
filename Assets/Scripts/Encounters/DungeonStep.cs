using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // One page of a dungeon run — a fight, a rest stop, a mid-run bonus treasure pickup,
    // or the guaranteed treasure room that always caps the run. Generated once per run by
    // DungeonGenerator, straight from the dungeon's own authored EncounterData.dungeonRooms
    // (no growth-formula scaling); DungeonView pages between these, resolving each the
    // first time its page is reached and just re-displaying the stored result on revisits.
    public readonly struct DungeonStep
    {
        // The step's position in the generated sequence (_steps/_pages index) — always a
        // clean 0..N-1 run, since only rooms that actually rolled in are included at all.
        public int DepthIndex { get; }
        public string Label { get; }
        public bool IsRestRoom { get; }

        // A mid-run bonus pickup (EncounterData.dungeonRooms' Treasure room type) —
        // grants a reward (gold + item, never XP) but the run continues afterward.
        // Distinct from IsFinalTreasureRoom below, which is the only one that ends the
        // run.
        public bool IsBonusTreasureRoom { get; }

        // The guaranteed, always-last step — every completed run ends here regardless of
        // which authored rooms rolled in. Ends the run and grants the full reward
        // (XP + gold + item).
        public bool IsFinalTreasureRoom { get; }

        // Meaningful only when IsBonusTreasureRoom or IsFinalTreasureRoom is true — 0/
        // empty otherwise. All pre-resolved at generation time (RewardGenerator.
        // RollLootTable/ResolveGoldReward), from the room's own loot table for a bonus
        // pickup, or EncounterData's own for the final room.
        public int XpReward { get; }
        public int GoldReward { get; }
        public ItemData[] RewardItems { get; }

        // Enemy combat stats for the Phase 8 simulation — meaningless unless this is a
        // Fight room (every other kind resolves instantly with a null CombatSimulator).
        public int EnemyHealth { get; }
        public int EnemyAttackDamage { get; }
        public float EnemyAttackSpeed { get; }

        // This room's own weaknesses (DungeonRoomData.damageTypeModifiers) — each Fight
        // room is independent, not shared across the dungeon.
        public DamageTypeModifier[] EnemyWeaknesses { get; }

        // This room's own creature portrait (DungeonRoomData.enemySprite) — null falls
        // back to StepColor, same convention as every other Sprite-or-flat-color field.
        public Sprite EnemySprite { get; }

        // Placeholder "art" for this page — a flat color, greybox convention, same as
        // every other fallback-color visual in this project.
        public Color StepColor { get; }

        public DungeonStep(int depthIndex, string label, bool isRestRoom, bool isBonusTreasureRoom,
            bool isFinalTreasureRoom, int xpReward, int goldReward, ItemData[] rewardItems,
            int enemyHealth, int enemyAttackDamage, float enemyAttackSpeed, DamageTypeModifier[] enemyWeaknesses,
            Sprite enemySprite, Color stepColor)
        {
            DepthIndex = depthIndex;
            Label = label;
            IsRestRoom = isRestRoom;
            IsBonusTreasureRoom = isBonusTreasureRoom;
            IsFinalTreasureRoom = isFinalTreasureRoom;
            XpReward = xpReward;
            GoldReward = goldReward;
            RewardItems = rewardItems;
            EnemyHealth = enemyHealth;
            EnemyAttackDamage = enemyAttackDamage;
            EnemyAttackSpeed = enemyAttackSpeed;
            EnemyWeaknesses = enemyWeaknesses;
            EnemySprite = enemySprite;
            StepColor = stepColor;
        }
    }
}
