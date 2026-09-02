using System;
using System.Collections.Generic;
using System.Linq;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Combat
{
    // Plain C# so the fight simulation is testable without a running scene. Phase 8:
    // outcome is no longer a single win-chance roll — both sides attack automatically at
    // their own attack speed, abilities cast off cooldown, and whoever's HP hits zero
    // first decides the fight. The player's real, persistent PlayerHealth pool is the
    // live stake (see ResolveFight) — no separate "combat HP."
    public static class CombatResolver
    {
        // Small per-swing randomness (Phase 8 design decision: not a fully deterministic
        // sim) — a flat percentage damage variance and a flat miss chance, independent of
        // stats. Both are early tuning knobs, easy to retune once the sim is played.
        private const float DamageVariance = 0.15f;
        private const float MissChance = 0.08f;

        // A win that ended below this fraction of max health reads as a "close call" in
        // the reveal UI — a post-fight read of how rough it was, not a pre-roll margin.
        private const float CloseCallHealthFraction = 0.25f;

        // Guards against a degenerate fight (e.g. authored 0-damage/0-speed content)
        // looping forever — not a real design case, just a safety net. If neither side
        // has died by this many beats, whoever has the healthier remaining fraction wins.
        private const int MaxBeats = 200;

        private const int UnarmedAttackDamage = 3;
        private const float UnarmedAttackSpeed = 0.6f;
        private const float MinIntervalSeconds = 0.05f;

        // Plain single-resolution encounters (Enemy/Treasure feed cards) — enemy combat
        // stats come straight from the authored asset.
        public static CombatResult Resolve(PlayerLoadout loadout, EncounterData data, PlayerHealth health, System.Random rng = null)
        {
            if (data.type == EncounterType.Treasure)
            {
                return ResolveTreasure();
            }

            return ResolveFight(loadout, data.enemyHealth, data.enemyAttackDamage, data.enemyAttackSpeed,
                health, rng ?? new System.Random());
        }

        // One page of a dungeon run — enemy stats come from the generated step (scaled
        // per depth by DungeonGenerator) instead of the raw asset. A treasure-room step
        // resolves as guaranteed loot regardless of the underlying EncounterData's own
        // type.
        public static CombatResult ResolveStep(PlayerLoadout loadout, DungeonStep step, PlayerHealth health, System.Random rng = null)
        {
            if (step.IsTreasureRoom)
            {
                return ResolveTreasure();
            }

            return ResolveFight(loadout, step.EnemyHealth, step.EnemyAttackDamage, step.EnemyAttackSpeed,
                health, rng ?? new System.Random());
        }

        // Unchanged from before Phase 8, per the "keep the indicator, iterate later on
        // its metric" decision — a rough difficulty heuristic for the feed badge,
        // completely decoupled from how ResolveFight actually plays out now.
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

        private static CombatResult ResolveTreasure()
        {
            return new CombatResult(true, Array.Empty<CombatBeat>(), 0, false);
        }

        private static CombatResult ResolveFight(PlayerLoadout loadout, int enemyMaxHealth, int enemyAttackDamage,
            float enemyAttackSpeed, PlayerHealth health, System.Random rng)
        {
            var leftHand = loadout.GetEquipped(EquipmentSlotType.LeftHand);
            var rightHand = loadout.GetEquipped(EquipmentSlotType.RightHand);
            var weapons = new List<ItemData>();
            if (leftHand?.category == ItemCategory.Weapon) weapons.Add(leftHand);
            if (rightHand?.category == ItemCategory.Weapon) weapons.Add(rightHand);

            var playerAttackDamage = weapons.Count > 0 ? weapons.Sum(w => w.powerValue) : UnarmedAttackDamage;
            var playerAttackSpeed = weapons.Count > 0 ? weapons.Average(w => w.attackSpeed) : UnarmedAttackSpeed;
            var playerIsRanged = weapons.Any(w => w.isRanged);

            var playerDefense = 0;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null && item.category != ItemCategory.Weapon)
                {
                    playerDefense += item.powerValue;
                }
            }

            var timers = new List<AttackTimer>
            {
                new(CombatActor.Player, IntervalFor(playerAttackSpeed), playerAttackDamage, playerIsRanged, null),
                new(CombatActor.Enemy, IntervalFor(enemyAttackSpeed), enemyAttackDamage, false, null)
            };

            foreach (var ability in loadout.GetEquippedWeaponAbilities())
            {
                if (ability.isActive && loadout.IsAbilityActive(ability.abilityName))
                {
                    // Abilities are timed by a flat cooldown in seconds (not a rate), so
                    // it's already the interval the event loop needs — no conversion.
                    timers.Add(new AttackTimer(CombatActor.Player, Mathf.Max(MinIntervalSeconds, ability.cooldownSeconds),
                        ability.powerBonus, true, ability.abilityName));
                }
            }

            var maxHealth = health.MaxHealth;
            var playerHp = health.CurrentHealth;
            var enemyHp = enemyMaxHealth;
            var totalDamageTaken = 0;
            var beats = new List<CombatBeat>();

            for (var i = 0; i < MaxBeats && playerHp > 0 && enemyHp > 0; i++)
            {
                var actor = timers[0];
                foreach (var timer in timers)
                {
                    if (timer.NextFireTime < actor.NextFireTime)
                    {
                        actor = timer;
                    }
                }

                if (rng.NextDouble() < MissChance)
                {
                    beats.Add(new CombatBeat(actor.Actor, actor.IsRanged, true, 0, actor.AbilityName));
                }
                else
                {
                    var variance = 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * DamageVariance;
                    var raw = Mathf.RoundToInt(actor.BaseDamage * variance);

                    int applied;
                    if (actor.Actor == CombatActor.Player)
                    {
                        applied = Mathf.Max(1, raw);
                        enemyHp = Mathf.Max(0, enemyHp - applied);
                    }
                    else
                    {
                        applied = Mathf.Max(1, raw - playerDefense);
                        playerHp = Mathf.Max(0, playerHp - applied);
                        totalDamageTaken += applied;
                    }

                    beats.Add(new CombatBeat(actor.Actor, actor.IsRanged, false, applied, actor.AbilityName));
                }

                actor.NextFireTime += actor.IntervalSeconds;
            }

            bool isVictory;
            if (enemyHp <= 0)
            {
                isVictory = true;
            }
            else if (playerHp <= 0)
            {
                isVictory = false;
            }
            else
            {
                // Safety-cap fallback: neither side died within MaxBeats — decide by
                // whoever's remaining health fraction is healthier.
                var playerFraction = maxHealth > 0 ? (float)playerHp / maxHealth : 0f;
                var enemyFraction = enemyMaxHealth > 0 ? (float)enemyHp / enemyMaxHealth : 0f;
                isVictory = playerFraction >= enemyFraction;
            }

            var wasCloseCall = isVictory && maxHealth > 0 && (float)playerHp / maxHealth < CloseCallHealthFraction;

            return new CombatResult(isVictory, beats, totalDamageTaken, wasCloseCall);
        }

        // Basic attacks are timed by attacks/sec; abilities are timed by a flat cooldown
        // in seconds — both funnel into the same "seconds between fires" the event loop
        // steps by.
        private static float IntervalFor(float attacksPerSecond)
        {
            return 1f / Mathf.Max(MinIntervalSeconds, attacksPerSecond);
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

        private sealed class AttackTimer
        {
            public readonly CombatActor Actor;
            public readonly float IntervalSeconds;
            public readonly int BaseDamage;
            public readonly bool IsRanged;
            public readonly string AbilityName;
            public float NextFireTime;

            public AttackTimer(CombatActor actor, float intervalSeconds, int baseDamage, bool isRanged, string abilityName)
            {
                Actor = actor;
                IntervalSeconds = intervalSeconds;
                BaseDamage = baseDamage;
                IsRanged = isRanged;
                AbilityName = abilityName;
                NextFireTime = 0f;
            }
        }
    }
}
