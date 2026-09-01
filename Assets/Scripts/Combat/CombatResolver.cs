using System;
using System.Collections.Generic;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;

namespace DoomScroller.Combat
{
    // Plain C# so outcome math is testable without a running scene. Outcome is driven
    // entirely by equipped stats/active abilities — no real-time player input (Phase 5).
    public static class CombatResolver
    {
        private const float ChallengeGrowthPerTier = 0.75f;

        public static CombatResult Resolve(PlayerLoadout loadout, EncounterData data, EncounterVariant variant, Random rng = null)
        {
            if (data.type == EncounterType.Treasure)
            {
                return ResolveTreasure(data);
            }

            return ResolveFight(loadout, data, variant, rng ?? new Random());
        }

        private static CombatResult ResolveTreasure(EncounterData data)
        {
            var lines = new List<string>
            {
                $"You approach the {data.title}.",
                "No guardians, no traps — just guaranteed loot.",
                "You claim it without a fight."
            };
            return new CombatResult(true, 0, 0, lines);
        }

        private static CombatResult ResolveFight(PlayerLoadout loadout, EncounterData data, EncounterVariant variant, Random rng)
        {
            var lines = new List<string> { $"You face {data.title} ({variant.DifficultyLabel})." };

            var playerPower = 0;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item == null)
                {
                    continue;
                }

                playerPower += item.powerValue;
                if (slot == EquipmentSlotType.Weapon)
                {
                    lines.Add($"You ready your {item.title}.");
                }
            }

            var weapon = loadout.GetEquipped(EquipmentSlotType.Weapon);
            if (weapon == null)
            {
                lines.Add("You have no weapon equipped — this could go badly.");
            }
            else if (weapon.abilities != null)
            {
                foreach (var ability in weapon.abilities)
                {
                    if (!loadout.IsAbilityActive(ability.abilityName))
                    {
                        continue;
                    }

                    playerPower += ability.powerBonus;
                    lines.Add($"You unleash {ability.abilityName}!");
                }
            }

            var challengePower = (int)MathF.Round(data.baseChallenge * (1f + variant.TierIndex * ChallengeGrowthPerTier));

            var winChance = Clamp01(0.5f + (playerPower - challengePower) / (2f * MathF.Max(challengePower, 1)));
            var isVictory = rng.NextDouble() < winChance;

            lines.Add(isVictory
                ? $"The {data.title} falls. Victory!"
                : $"You're overwhelmed by the {data.title}. Defeat.");

            return new CombatResult(isVictory, playerPower, challengePower, lines);
        }

        private static float Clamp01(float value)
        {
            return value < 0.05f ? 0.05f : value > 0.95f ? 0.95f : value;
        }
    }
}
