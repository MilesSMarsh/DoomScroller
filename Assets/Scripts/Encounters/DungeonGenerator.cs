using System.Collections.Generic;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Plain C# so dungeon scaling is testable without a running scene.
    // Builds one dungeon run's step sequence: escalating enemy depths, an optional rest
    // stop, always capped by a guaranteed treasure room. Reward rarity per fight step is
    // its RewardRarity (Common/Uncommon/Rare, one per EnemyDepthCount depth — never
    // Legendary, dungeons cap at Rare), independent of the step's actual DepthIndex/list
    // position so inserting a rest step never shifts a later fight's intended rarity —
    // the treasure room's own reward always clamps to the best tier regardless.
    public static class DungeonGenerator
    {
        private const int EnemyDepthCount = 3;
        private const float StatGrowthPerDepth = 0.75f;
        private const float XpGrowthPerDepth = 0.75f;
        private const float RestRoomChance = 0.5f;

        // One flat placeholder color per depth, deepening/reddening as the dungeon gets
        // harder — greybox "art," same convention as every other fallback-color visual
        // in this project.
        private static readonly Color[] DepthColors =
        {
            new(0.55f, 0.25f, 0.2f, 1f),
            new(0.5f, 0.2f, 0.35f, 1f),
            new(0.35f, 0.15f, 0.45f, 1f)
        };

        private static readonly Color TreasureColor = new(0.85f, 0.7f, 0.2f, 1f);
        // Calming teal — reads as "safe," distinct from every combat depth and from gold.
        private static readonly Color RestColor = new(0.2f, 0.5f, 0.45f, 1f);

        public static IReadOnlyList<DungeonStep> Generate(EncounterData data, System.Random rng = null)
        {
            rng ??= new System.Random();

            // Built unordered first (fight steps, then one optional rest step spliced in
            // after a random fight), then re-walked below to assign clean sequential
            // DepthIndex values matching final list order.
            var steps = new List<DungeonStep>(EnemyDepthCount + 2);

            // Picked once per run — never before the first fight, never after the
            // treasure room — and only actually included per RestRoomChance below.
            var restAfterDepth = rng.Next(EnemyDepthCount);

            for (var i = 0; i < EnemyDepthCount; i++)
            {
                var growth = 1f + i * StatGrowthPerDepth;
                var enemyHealth = Mathf.RoundToInt(data.enemyHealth * growth);
                var enemyAttackDamage = Mathf.RoundToInt(data.enemyAttackDamage * growth);
                // Deeper enemies hit harder and have more HP, not faster — keeps fight
                // pacing sane at depth.
                var enemyAttackSpeed = data.enemyAttackSpeed;
                var xp = Mathf.RoundToInt(data.baseXpReward * (1f + i * XpGrowthPerDepth));
                var rewardRarity = (ItemRarity)i;
                var rewardSummary = $"{xp} XP + {rewardRarity} Gear";
                steps.Add(new DungeonStep(0, $"Depth {i + 1}", rewardSummary, xp, rewardRarity,
                    enemyHealth, enemyAttackDamage, enemyAttackSpeed, data.damageTypeModifiers, false, false, DepthColors[i]));

                // Splice the rest step in right after whichever fight depth was chosen —
                // never before the first fight, never after the treasure room.
                if (i == restAfterDepth && rng.NextDouble() < RestRoomChance)
                {
                    steps.Add(new DungeonStep(0, "Rest", "Full HP restore, no combat", 0, ItemRarity.Common,
                        0, 0, 0f, null, false, true, RestColor));
                }
            }

            var treasureXp = Mathf.RoundToInt(data.baseXpReward * (1f + EnemyDepthCount * XpGrowthPerDepth));
            var treasureReward = $"{treasureXp} XP + guaranteed Rare loot";
            steps.Add(new DungeonStep(0, "Treasure Room", treasureReward, treasureXp, ItemRarity.Rare,
                0, 0, 0f, null, true, false, TreasureColor));

            // Re-stamp DepthIndex to the step's real position — the placeholder 0s above
            // only exist because DungeonStep bakes DepthIndex in at construction, before
            // final list order (and whether a rest step landed) is known.
            for (var i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                steps[i] = new DungeonStep(i, s.Label, s.RewardSummary, s.XpReward, s.RewardRarity,
                    s.EnemyHealth, s.EnemyAttackDamage, s.EnemyAttackSpeed, s.EnemyWeaknesses,
                    s.IsTreasureRoom, s.IsRestRoom, s.StepColor);
            }

            return steps;
        }
    }
}
