using System;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;

namespace DoomScroller.Combat
{
    // Builds a CombatSimulator for a given encounter/step, and holds the feed badge's
    // predicted-odds heuristic — completely separate concerns that happen to share this
    // file for historical reasons. The heuristic is intentionally decoupled from how a
    // fight actually resolves; see CombatSimulator for the real fight math.
    public static class CombatResolver
    {
        // Plain single-resolution encounters (Enemy/Treasure feed cards) — enemy combat
        // stats come straight from the authored asset. Null for Treasure — there's no
        // fight to simulate; the caller treats a null simulator as an instant win.
        public static CombatSimulator CreateSimulator(PlayerLoadout loadout, PlayerStats stats, GameSettings settings,
            EncounterData data, int playerStartHp, Random rng = null)
        {
            if (data.type == EncounterType.Treasure)
            {
                return null;
            }

            return new CombatSimulator(loadout, stats, settings, playerStartHp, data.enemyHealth,
                data.enemyAttackDamage, data.enemyAttackSpeed, rng ?? new Random());
        }

        // One page of a dungeon run — enemy stats come from the generated step (scaled
        // per depth by DungeonGenerator) instead of the raw asset. Null for the
        // treasure-room step, same reasoning as above.
        public static CombatSimulator CreateSimulatorForStep(PlayerLoadout loadout, PlayerStats stats,
            GameSettings settings, DungeonStep step, int playerStartHp, Random rng = null)
        {
            if (step.IsTreasureRoom)
            {
                return null;
            }

            return new CombatSimulator(loadout, stats, settings, playerStartHp, step.EnemyHealth,
                step.EnemyAttackDamage, step.EnemyAttackSpeed, rng ?? new Random());
        }

        // A rough difficulty heuristic for the feed badge — completely decoupled from
        // how CombatSimulator actually plays out. Kept exactly as it was pre-Phase-8;
        // iterate on it separately if it ever needs to.
        public static float PredictWinChance(PlayerLoadout loadout, EncounterData data)
        {
            // Neither ever fights — Tavern is a non-combat social hub (see TavernView).
            if (data.type == EncounterType.Treasure || data.type == EncounterType.Tavern)
            {
                return 1f;
            }

            var playerPower = ComputePlayerPower(loadout);
            return ComputeWinChance(playerPower, data.baseChallenge);
        }

        public static float PredictCloseCallChance(PlayerLoadout loadout, EncounterData data)
        {
            if (data.type == EncounterType.Treasure || data.type == EncounterType.Tavern)
            {
                return 0f;
            }

            var winChance = PredictWinChance(loadout, data);
            return MathF.Min(0.15f, winChance);
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
