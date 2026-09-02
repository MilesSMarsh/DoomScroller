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
        // A win whose roll landed within this margin of the loss boundary counts as a
        // "close call" — you still won, but it cost you (see ResolveFight).
        private const float CloseCallMargin = 0.15f;
        private const int DefeatDamage = 20;
        private const int CloseCallDamage = 8;

        // Plain single-resolution encounters (Enemy/Treasure feed cards) — no
        // difficulty choice any more, challenge comes straight from the asset.
        public static CombatResult Resolve(PlayerLoadout loadout, EncounterData data, Random rng = null)
        {
            if (data.type == EncounterType.Treasure)
            {
                return ResolveTreasure(data);
            }

            return ResolveFight(loadout, data, data.baseChallenge, "", rng ?? new Random());
        }

        // One page of a dungeon run — challenge/label come from the generated step
        // instead of the raw asset, and a treasure-room step resolves as guaranteed
        // loot regardless of the underlying EncounterData's own type (it's still an
        // Enemy-type asset; the *step* is what says this particular page is the prize).
        public static CombatResult ResolveStep(PlayerLoadout loadout, EncounterData data, DungeonStep step, Random rng = null)
        {
            if (step.IsTreasureRoom)
            {
                return ResolveTreasure(data);
            }

            return ResolveFight(loadout, data, step.ChallengePower, step.Label, rng ?? new Random());
        }

        // Same win-chance math as ResolveFight, without rolling — lets the feed show a
        // predicted percent/color for the entry-level fight before entering (a dungeon's
        // first depth uses the same baseChallenge as a plain encounter would).
        public static float PredictWinChance(PlayerLoadout loadout, EncounterData data)
        {
            if (data.type == EncounterType.Treasure)
            {
                return 1f;
            }

            var playerPower = ComputePlayerPower(loadout);
            return ComputeWinChance(playerPower, data.baseChallenge);
        }

        // Of that win chance, how much of it is a "close call" that still costs health
        // (see CloseCallMargin/ResolveFight) — the feed shows this as a second, smaller
        // percentage alongside the overall win chance. 0 for Treasure (never at risk).
        public static float PredictCloseCallChance(PlayerLoadout loadout, EncounterData data)
        {
            if (data.type == EncounterType.Treasure)
            {
                return 0f;
            }

            var winChance = PredictWinChance(loadout, data);
            return MathF.Min(CloseCallMargin, winChance);
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

        private static CombatResult ResolveFight(PlayerLoadout loadout, EncounterData data, int challengePower, string label, Random rng)
        {
            var lines = new List<string>
            {
                string.IsNullOrEmpty(label) ? $"You face {data.title}." : $"You face {data.title} ({label})."
            };
            var playerPower = ComputePlayerPower(loadout);

            var leftHand = loadout.GetEquipped(EquipmentSlotType.LeftHand);
            var rightHand = loadout.GetEquipped(EquipmentSlotType.RightHand);
            var hasWeapon = leftHand?.category == ItemCategory.Weapon || rightHand?.category == ItemCategory.Weapon;
            if (!hasWeapon)
            {
                lines.Add("You have no weapon equipped — this could go badly.");
            }
            else
            {
                if (leftHand?.category == ItemCategory.Weapon)
                {
                    lines.Add($"You ready your {leftHand.title}.");
                }

                if (rightHand?.category == ItemCategory.Weapon)
                {
                    lines.Add($"You ready your {rightHand.title}.");
                }
            }

            foreach (var ability in loadout.GetEquippedWeaponAbilities())
            {
                if (ability.isActive && loadout.IsAbilityActive(ability.abilityName))
                {
                    lines.Add($"You unleash {ability.abilityName}!");
                }
            }

            var winChance = ComputeWinChance(playerPower, challengePower);
            var roll = rng.NextDouble();
            var isVictory = roll < winChance;

            // How close was the roll to the losing side of the threshold, regardless of
            // outcome — a win that barely cleared it is a "close call"; a loss is always
            // a loss (Treasure aside, there's no such thing as a "close call" defeat here).
            var wasCloseCall = isVictory && roll >= winChance - CloseCallMargin;
            var healthDamage = isVictory ? (wasCloseCall ? CloseCallDamage : 0) : DefeatDamage;

            if (isVictory)
            {
                lines.Add(wasCloseCall
                    ? $"The {data.title} falls, but not before landing a hit. A costly victory."
                    : $"The {data.title} falls. Victory!");
            }
            else
            {
                lines.Add($"You're overwhelmed by the {data.title}. Defeat.");
            }

            return new CombatResult(isVictory, playerPower, challengePower, lines, healthDamage, wasCloseCall);
        }

        private static int ComputePlayerPower(PlayerLoadout loadout)
        {
            var playerPower = 0;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null)
                {
                    playerPower += item.powerValue;
                }
            }

            foreach (var ability in loadout.GetEquippedWeaponAbilities())
            {
                if (loadout.IsAbilityEffective(ability))
                {
                    playerPower += ability.powerBonus;
                }
            }

            return playerPower;
        }

        private static float ComputeWinChance(int playerPower, int challengePower)
        {
            return Clamp01(0.5f + (playerPower - challengePower) / (2f * MathF.Max(challengePower, 1)));
        }

        private static float Clamp01(float value)
        {
            return value < 0.05f ? 0.05f : value > 0.95f ? 0.95f : value;
        }
    }
}
