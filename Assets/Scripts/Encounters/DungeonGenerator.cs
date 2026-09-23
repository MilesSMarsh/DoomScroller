using System.Collections.Generic;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Plain C# so dungeon scaling is testable without a running scene.
    // Builds one dungeon run's step sequence straight from the dungeon's own authored
    // EncounterData.dungeonRooms — each room independently rolls its own appearChance,
    // and an included room's stats/reward are exactly what was authored, no growth-
    // formula scaling. A guaranteed reward room always caps the run, appended after
    // every authored room regardless of which of them rolled in.
    public static class DungeonGenerator
    {
        // A handful of placeholder colors that cycle by position — greybox "art," same
        // convention as every other fallback-color visual in this project. Room count is
        // author-controlled now (not a fixed depth count), so this cycles instead of
        // indexing a fixed-size array.
        private static readonly Color[] FightColors =
        {
            new(0.55f, 0.25f, 0.2f, 1f),
            new(0.5f, 0.2f, 0.35f, 1f),
            new(0.35f, 0.15f, 0.45f, 1f),
            new(0.3f, 0.3f, 0.15f, 1f)
        };

        private static readonly Color TreasureColor = new(0.85f, 0.7f, 0.2f, 1f);
        // Calming teal — reads as "safe," distinct from every combat depth and from gold.
        private static readonly Color RestColor = new(0.2f, 0.5f, 0.45f, 1f);

        public static IReadOnlyList<DungeonStep> Generate(EncounterData data, System.Random rng = null)
        {
            rng ??= new System.Random();

            var steps = new List<DungeonStep>();
            var fightCount = 0;

            foreach (var room in data.dungeonRooms ?? System.Array.Empty<DungeonRoomData>())
            {
                if (room == null || rng.NextDouble() * 100.0 >= room.appearChance)
                {
                    continue;
                }

                switch (room.type)
                {
                    case DungeonRoomType.Rest:
                        steps.Add(new DungeonStep(0, string.IsNullOrEmpty(room.label) ? "Rest" : room.label,
                            isRestRoom: true, isBonusTreasureRoom: false, isFinalTreasureRoom: false,
                            xpReward: 0, goldReward: 0, rewardItems: null,
                            enemyHealth: 0, enemyAttackDamage: 0, enemyAttackSpeed: 0f, enemyWeaknesses: null,
                            enemySprite: null, stepColor: RestColor));
                        break;

                    case DungeonRoomType.Treasure:
                        var bonusItems = RewardGenerator.RollLootTable(room.lootTable, rng);
                        var bonusGold = RewardGenerator.ResolveGoldReward(room.goldReward, 0);
                        steps.Add(new DungeonStep(0, string.IsNullOrEmpty(room.label) ? "Treasure!" : room.label,
                            isRestRoom: false, isBonusTreasureRoom: true, isFinalTreasureRoom: false,
                            xpReward: 0, goldReward: bonusGold, rewardItems: bonusItems,
                            enemyHealth: 0, enemyAttackDamage: 0, enemyAttackSpeed: 0f, enemyWeaknesses: null,
                            enemySprite: null, stepColor: TreasureColor));
                        break;

                    default:
                        fightCount++;
                        var label = string.IsNullOrEmpty(room.label) ? $"Depth {fightCount}" : room.label;
                        steps.Add(new DungeonStep(0, label,
                            isRestRoom: false, isBonusTreasureRoom: false, isFinalTreasureRoom: false,
                            xpReward: 0, goldReward: 0, rewardItems: null,
                            enemyHealth: room.enemyHealth, enemyAttackDamage: room.enemyAttackDamage,
                            enemyAttackSpeed: room.enemyAttackSpeed, enemyWeaknesses: room.damageTypeModifiers,
                            enemySprite: room.enemySprite, stepColor: FightColors[(fightCount - 1) % FightColors.Length]));
                        break;
                }
            }

            var finalItems = RewardGenerator.RollLootTable(data.lootTable, rng);
            var finalGold = RewardGenerator.ResolveGoldReward(data.goldReward, data.baseXpReward);
            steps.Add(new DungeonStep(0, "Treasure Room",
                isRestRoom: false, isBonusTreasureRoom: false, isFinalTreasureRoom: true,
                xpReward: data.baseXpReward, goldReward: finalGold, rewardItems: finalItems,
                enemyHealth: 0, enemyAttackDamage: 0, enemyAttackSpeed: 0f, enemyWeaknesses: null,
                enemySprite: null, stepColor: TreasureColor));

            // Re-stamp DepthIndex to the step's real position — the placeholder 0s above
            // only exist because DungeonStep bakes DepthIndex in at construction, before
            // final list order (and which rooms actually rolled in) is known.
            for (var i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                steps[i] = new DungeonStep(i, s.Label, s.IsRestRoom, s.IsBonusTreasureRoom, s.IsFinalTreasureRoom,
                    s.XpReward, s.GoldReward, s.RewardItems, s.EnemyHealth, s.EnemyAttackDamage, s.EnemyAttackSpeed,
                    s.EnemyWeaknesses, s.EnemySprite, s.StepColor);
            }

            return steps;
        }
    }
}
