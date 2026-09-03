using System;
using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Combat
{
    // A stateful, resumable fight — the event-loop math that used to live in
    // CombatResolver.ResolveFight, restructured so a revive potion can keep the same
    // fight going instead of the outcome being locked in the moment the player dies.
    // Nothing here touches PlayerHealth; PlayerHp/PlayerStartHp are this fight's own
    // ephemeral instance, seeded once at construction from whatever the caller passed
    // in as the starting HP.
    public class CombatSimulator
    {
        // Small per-swing randomness — a flat percentage damage variance and a flat
        // miss chance, independent of stats. Both are early tuning knobs.
        private const float DamageVariance = 0.15f;
        private const float MissChance = 0.08f;

        // Guards against a degenerate fight (e.g. authored 0-damage/0-speed content, or
        // a long chain of revives) looping forever — a *global* budget across every
        // RunUntilDeathOrVictory call on this instance, not reset by Revive. If neither
        // side has died by the time it's exhausted, whoever has the healthier
        // remaining fraction is forced to win.
        private const int MaxBeats = 200;

        private const int UnarmedAttackDamage = 3;
        private const float UnarmedAttackSpeed = 0.6f;
        private const float MinIntervalSeconds = 0.05f;

        // Global stat effects that aren't weapon-specific (Strength/Dexterity/Spirit
        // are expressed purely through each weapon's own StatScaling entries instead —
        // there's no single global coefficient for those three).
        private const float EnduranceAttackSpeedPerPoint = 0.015f;
        private const float IntelligenceCooldownDivisorPerPoint = 0.03f;

        private readonly List<AttackTimer> _timers;
        private readonly Dictionary<string, ManualAbility> _manualAbilities = new();
        private readonly int _playerDefense;
        private readonly System.Random _rng;
        private int _beatsRun;

        public int PlayerHp { get; private set; }
        public int PlayerStartHp { get; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; }

        public bool IsPlayerDead => PlayerHp <= 0;
        public bool IsEnemyDead => EnemyHp <= 0;

        public CombatSimulator(PlayerLoadout loadout, PlayerStats stats, GameSettings settings, int playerStartHp,
            int enemyMaxHealth, int enemyAttackDamage, float enemyAttackSpeed, System.Random rng)
        {
            _rng = rng;
            PlayerHp = PlayerStartHp = Mathf.Max(1, playerStartHp);
            EnemyHp = EnemyMaxHp = enemyMaxHealth;

            var leftHand = loadout.GetEquipped(EquipmentSlotType.LeftHand);
            var rightHand = loadout.GetEquipped(EquipmentSlotType.RightHand);
            var weapons = new List<ItemData>();
            if (leftHand?.category == ItemCategory.Weapon) weapons.Add(leftHand);
            if (rightHand?.category == ItemCategory.Weapon) weapons.Add(rightHand);

            var playerAttackDamage = weapons.Count > 0
                ? weapons.Sum(w => w.powerValue + ScalingBonus(w, stats))
                : UnarmedAttackDamage;
            var playerAttackSpeed = weapons.Count > 0 ? weapons.Average(w => w.attackSpeed) : UnarmedAttackSpeed;
            playerAttackSpeed += stats.Get(PlayerStat.Endurance) * EnduranceAttackSpeedPerPoint;
            var playerIsRanged = weapons.Any(w => w.isRanged);

            _playerDefense = 0;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null && item.category != ItemCategory.Weapon)
                {
                    _playerDefense += item.powerValue;
                }
            }

            _timers = new List<AttackTimer>
            {
                new(CombatActor.Player, IntervalFor(playerAttackSpeed), playerAttackDamage, playerIsRanged, null, 0f),
                new(CombatActor.Enemy, IntervalFor(enemyAttackSpeed), enemyAttackDamage, false, null, 0f)
            };

            var cooldownDivisor = 1f + stats.Get(PlayerStat.Intelligence) * IntelligenceCooldownDivisorPerPoint;
            foreach (var (weapon, ability) in loadout.GetEquippedWeaponAbilityPairs())
            {
                if (!ability.isActive || !loadout.IsAbilityActive(ability.abilityName))
                {
                    continue;
                }

                // An ability hits harder than a basic swing while scaling identically
                // off whatever the weapon scales off: the same base-power + stat-scaling
                // math a basic attack uses, plus the ability's own bonus on top.
                var abilityDamage = weapon.powerValue + ScalingBonus(weapon, stats) + ability.powerBonus;
                var cooldown = Mathf.Max(MinIntervalSeconds, ability.cooldownSeconds / cooldownDivisor);

                if (settings.ManualAbilityCasting)
                {
                    // Excluded from the auto-timer loop entirely — CombatPlaybackView
                    // drives this one in real time via AdvanceManualCooldowns/
                    // CastManualAbility instead, since its trigger moment depends on a
                    // live player tap rather than anything RunUntilDeathOrVictory can
                    // precompute in one batch.
                    _manualAbilities[ability.abilityName] = new ManualAbility(abilityDamage, cooldown);
                }
                else
                {
                    // Starts ON cooldown (NextFireTime = cooldown, not 0) — every other
                    // timer above starts ready, abilities deliberately don't.
                    _timers.Add(new AttackTimer(CombatActor.Player, cooldown, abilityDamage, true,
                        ability.abilityName, cooldown));
                }
            }
        }

        // Strength/Dexterity/Spirit have no single global coefficient — each weapon
        // defines which stat(s) it scales off and by how much, so a weapon concept can
        // scale off more than one (a magic sword off Strength and Spirit, a heavy bow
        // off Dexterity and Strength) and different tiers can scale differently.
        private static int ScalingBonus(ItemData weapon, PlayerStats stats)
        {
            if (weapon.statScalingEntries == null || weapon.statScalingEntries.Length == 0)
            {
                return 0;
            }

            var bonus = 0f;
            foreach (var entry in weapon.statScalingEntries)
            {
                bonus += stats.Get(entry.stat) * entry.coefficient;
            }

            return Mathf.RoundToInt(bonus);
        }

        // Runs beats until someone's dead, or this instance's whole MaxBeats budget is
        // exhausted (across every call, including post-revive continuations) — in
        // which case the weaker side is forced to 0 so the caller only ever has to
        // check IsPlayerDead/IsEnemyDead, never a third "nobody died" state.
        public List<CombatBeat> RunUntilDeathOrVictory()
        {
            var beats = new List<CombatBeat>();

            while (_beatsRun < MaxBeats && PlayerHp > 0 && EnemyHp > 0)
            {
                _beatsRun++;

                var actor = _timers[0];
                foreach (var timer in _timers)
                {
                    if (timer.NextFireTime < actor.NextFireTime)
                    {
                        actor = timer;
                    }
                }

                beats.Add(ResolveBeat(actor.Actor, actor.BaseDamage, actor.IsRanged, actor.AbilityName));
                actor.NextFireTime += actor.IntervalSeconds;
            }

            if (PlayerHp > 0 && EnemyHp > 0)
            {
                // Safety-cap fallback: force a decision by remaining HP fraction.
                var playerFraction = PlayerStartHp > 0 ? (float)PlayerHp / PlayerStartHp : 0f;
                var enemyFraction = EnemyMaxHp > 0 ? (float)EnemyHp / EnemyMaxHp : 0f;
                if (playerFraction >= enemyFraction)
                {
                    EnemyHp = 0;
                }
                else
                {
                    PlayerHp = 0;
                }
            }

            return beats;
        }

        // Shared miss/variance/damage-application math — used by the auto-timer beat
        // loop above and by a manual ability's on-tap cast below, so both play out
        // identically.
        private CombatBeat ResolveBeat(CombatActor actor, int baseDamage, bool isRanged, string abilityName)
        {
            if (_rng.NextDouble() < MissChance)
            {
                return new CombatBeat(actor, isRanged, true, 0, abilityName);
            }

            var variance = 1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * DamageVariance;
            var raw = Mathf.RoundToInt(baseDamage * variance);

            int applied;
            if (actor == CombatActor.Player)
            {
                applied = Mathf.Max(1, raw);
                EnemyHp = Mathf.Max(0, EnemyHp - applied);
            }
            else
            {
                applied = Mathf.Max(1, raw - _playerDefense);
                PlayerHp = Mathf.Max(0, PlayerHp - applied);
            }

            return new CombatBeat(actor, isRanged, false, applied, abilityName);
        }

        // A revive potion's effect — the fight continues from here with the same enemy
        // HP and timers, just a fresh player HP.
        public void Revive(int newHp)
        {
            PlayerHp = Mathf.Max(1, newHp);
        }

        // Real-time tick for manual abilities only — the one part of this otherwise
        // beat/turn-based simulator driven by wall-clock time, because a manual
        // ability's readiness is inherently a live, player-facing thing rather than
        // something resolved in a precomputed batch. No-op once the fight has ended.
        public void AdvanceManualCooldowns(float deltaTime)
        {
            if (_manualAbilities.Count == 0 || IsPlayerDead || IsEnemyDead)
            {
                return;
            }

            foreach (var ability in _manualAbilities.Values)
            {
                ability.RemainingSeconds = Mathf.Max(0f, ability.RemainingSeconds - deltaTime);
            }
        }

        public IReadOnlyList<ManualAbilityState> GetManualAbilityStates()
        {
            var states = new List<ManualAbilityState>(_manualAbilities.Count);
            foreach (var kvp in _manualAbilities)
            {
                var ability = kvp.Value;
                var fraction = ability.CooldownSeconds > 0f ? ability.RemainingSeconds / ability.CooldownSeconds : 0f;
                states.Add(new ManualAbilityState(kvp.Key, Mathf.Clamp01(fraction), ability.RemainingSeconds <= 0f));
            }

            return states;
        }

        // Null if the ability isn't tracked as manual, isn't off cooldown yet, or the
        // fight has already ended — the caller (CombatPlaybackView) only calls this in
        // response to a tap on an already-interactable button, so those are defensive,
        // not expected in normal play.
        public CombatBeat? CastManualAbility(string abilityName)
        {
            if (IsPlayerDead || IsEnemyDead || !_manualAbilities.TryGetValue(abilityName, out var ability) ||
                ability.RemainingSeconds > 0f)
            {
                return null;
            }

            ability.RemainingSeconds = ability.CooldownSeconds;
            return ResolveBeat(CombatActor.Player, ability.Damage, true, abilityName);
        }

        private static float IntervalFor(float attacksPerSecond)
        {
            return 1f / Mathf.Max(MinIntervalSeconds, attacksPerSecond);
        }

        public readonly struct ManualAbilityState
        {
            public readonly string Name;
            public readonly float RemainingFraction;
            public readonly bool IsReady;

            public ManualAbilityState(string name, float remainingFraction, bool isReady)
            {
                Name = name;
                RemainingFraction = remainingFraction;
                IsReady = isReady;
            }
        }

        private sealed class ManualAbility
        {
            public readonly int Damage;
            public readonly float CooldownSeconds;
            public float RemainingSeconds;

            public ManualAbility(int damage, float cooldownSeconds)
            {
                Damage = damage;
                CooldownSeconds = cooldownSeconds;
                RemainingSeconds = cooldownSeconds;
            }
        }

        private sealed class AttackTimer
        {
            public readonly CombatActor Actor;
            public readonly float IntervalSeconds;
            public readonly int BaseDamage;
            public readonly bool IsRanged;
            public readonly string AbilityName;
            public float NextFireTime;

            public AttackTimer(CombatActor actor, float intervalSeconds, int baseDamage, bool isRanged,
                string abilityName, float initialDelay)
            {
                Actor = actor;
                IntervalSeconds = intervalSeconds;
                BaseDamage = baseDamage;
                IsRanged = isRanged;
                AbilityName = abilityName;
                NextFireTime = initialDelay;
            }
        }
    }
}
