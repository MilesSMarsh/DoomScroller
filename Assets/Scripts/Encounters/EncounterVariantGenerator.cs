using System.Collections.Generic;
using DoomScroller.Feed;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Plain C# so difficulty scaling is testable without a running scene.
    // Generates progressively harder/better-reward variants of one encounter,
    // starting from its feed-entry difficulty.
    public static class EncounterVariantGenerator
    {
        private static readonly string[] DifficultyLabels = { "Standard", "Hard", "Brutal" };
        private static readonly string[] GearRarities = { "Common", "Uncommon", "Rare" };
        private const float XpGrowthPerTier = 0.75f;

        public static IReadOnlyList<EncounterVariant> Generate(EncounterData data)
        {
            // Treasure is guaranteed loot, not a fight — there's no difficulty to dial up,
            // so it gets a single fixed variant and the sideways swipe does nothing.
            var tierCount = data.type == EncounterType.Treasure ? 1 : DifficultyLabels.Length;

            var variants = new List<EncounterVariant>(tierCount);
            for (var i = 0; i < tierCount; i++)
            {
                var xp = Mathf.RoundToInt(data.baseXpReward * (1f + i * XpGrowthPerTier));
                var rewardSummary = data.type == EncounterType.Treasure
                    ? $"{xp} XP + guaranteed {GearRarities[i]} loot"
                    : $"{xp} XP + {GearRarities[i]} Gear";
                variants.Add(new EncounterVariant(i, DifficultyLabels[i], rewardSummary, xp));
            }

            return variants;
        }
    }
}
