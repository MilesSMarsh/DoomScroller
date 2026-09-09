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
        public static CombatSimulator CreateSimulator(PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings,
            EncounterData data, int playerStartHp, Random rng = null)
        {
            if (data.type == EncounterType.Treasure)
            {
                return null;
            }

            return new CombatSimulator(loadout, attributes, settings, playerStartHp, data.enemyHealth,
                data.enemyAttackDamage, data.enemyAttackSpeed, rng ?? new Random());
        }

        // One page of a dungeon run — enemy stats come from the generated step (scaled
        // per depth by DungeonGenerator) instead of the raw asset. Null for the
        // treasure-room step, same reasoning as above.
        public static CombatSimulator CreateSimulatorForStep(PlayerLoadout loadout, PlayerAttributes attributes,
            GameSettings settings, DungeonStep step, int playerStartHp, Random rng = null)
        {
            // Both resolve instantly with no fight — CombatPlaybackView.Play already
            // treats a null simulator as a guaranteed win.
            if (step.IsTreasureRoom || step.IsRestRoom)
            {
                return null;
            }

            return new CombatSimulator(loadout, attributes, settings, playerStartHp, step.EnemyHealth,
                step.EnemyAttackDamage, step.EnemyAttackSpeed, rng ?? new Random());
        }

        // Time-to-kill race: how long the player would take to kill the enemy versus how
        // long the enemy would take to kill the player, both as steady-state DPS with
        // variance/misses ignored ("perfect rng" — CombatSimulator.EstimatePlayerDps is
        // the exact same math a real fight seeds its timers from, so this can never
        // silently drift out of sync with how a fight actually plays out). A large
        // discrepancy either direction reads as clearly easy or hard; a close one reads
        // as an even fight, regardless of how big or small the raw numbers involved are.
        private const float WinChanceSteepness = 2f;
        private const float MinDps = 0.01f;

        public static float PredictWinChance(PlayerLoadout loadout, PlayerAttributes attributes, PlayerHealth health, EncounterData data)
        {
            // None of these ever fight — Tavern is a non-combat social hub (see
            // TavernView), Resource nodes are an instant gather (see ResourceEncounterView),
            // Landing is the blank first-open card (see FeedController).
            if (data.type == EncounterType.Treasure || data.type == EncounterType.Tavern ||
                data.type == EncounterType.Resource || data.type == EncounterType.Landing)
            {
                return 1f;
            }

            return ComputeWinChance(loadout, attributes, health.CurrentMaxHealth, data.enemyHealth, data.enemyAttackDamage,
                data.enemyAttackSpeed);
        }

        public static float PredictCloseCallChance(PlayerLoadout loadout, PlayerAttributes attributes, PlayerHealth health, EncounterData data)
        {
            if (data.type == EncounterType.Treasure || data.type == EncounterType.Tavern ||
                data.type == EncounterType.Resource || data.type == EncounterType.Landing)
            {
                return 0f;
            }

            var winChance = PredictWinChance(loadout, attributes, health, data);
            // Peaks at an even (50/50) fight and fades out toward either lopsided edge —
            // an early tuning knob, same as CombatSimulator's own DamageVariance/MissChance.
            return MathF.Max(0f, 0.3f - MathF.Abs(winChance - 0.5f) * 1.2f);
        }

        private static float ComputeWinChance(PlayerLoadout loadout, PlayerAttributes attributes, int playerHealth,
            int enemyHealth, int enemyAttackDamage, float enemyAttackSpeed)
        {
            var playerDps = MathF.Max(MinDps, CombatSimulator.EstimatePlayerDps(loadout, attributes));
            var playerDefense = CombatSimulator.EstimatePlayerDefense(loadout);
            // Passive WeakenDamage/SlowAttackSpeed weapons lower the enemy's own stats
            // here too — same multipliers the real fight applies in CombatSimulator's
            // constructor, so the badge can't drift from what the fight actually does.
            var effectiveEnemyDamage = enemyAttackDamage * CombatSimulator.EstimateEnemyDamageMultiplier(loadout);
            var effectiveEnemySpeed = enemyAttackSpeed * CombatSimulator.EstimateEnemySpeedMultiplier(loadout);
            var enemyDps = MathF.Max(1, effectiveEnemyDamage - playerDefense) * MathF.Max(MinDps, effectiveEnemySpeed);

            var playerTimeToKill = enemyHealth / playerDps;
            var enemyTimeToKill = MathF.Max(1, playerHealth) / enemyDps;

            var logRatio = MathF.Log(enemyTimeToKill / playerTimeToKill);
            return Clamp01(1f / (1f + MathF.Exp(-WinChanceSteepness * logRatio)));
        }

        private static float Clamp01(float value)
        {
            return value < 0.05f ? 0.05f : value > 0.95f ? 0.95f : value;
        }
    }
}
