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

        // Global attribute effects that aren't weapon-specific (Constitution's/Agility's/
        // Spirit's damage halves are expressed purely through each weapon's own
        // AttributeScaling entries instead — there's no single global coefficient for those).
        private const float AgilityAttackSpeedPerPoint = 0.015f;
        private const float SpiritCooldownDivisorPerPoint = 0.03f;

        private readonly List<AttackTimer> _timers;
        private readonly Dictionary<string, ManualAbility> _manualAbilities = new();
        private readonly Dictionary<string, StatusEffectData[]> _abilityCastEffects;
        private readonly List<TimedEffect> _activeEffects = new();
        private readonly int _playerDefense;
        private readonly System.Random _rng;
        private int _beatsRun;
        private float _currentTime;

        public int PlayerHp { get; private set; }
        public int PlayerStartHp { get; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; }

        public bool IsPlayerDead => PlayerHp <= 0;
        public bool IsEnemyDead => EnemyHp <= 0;

        public CombatSimulator(PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings, int playerStartHp,
            int enemyMaxHealth, int enemyAttackDamage, float enemyAttackSpeed, System.Random rng)
        {
            _rng = rng;
            PlayerHp = PlayerStartHp = Mathf.Max(1, playerStartHp);
            EnemyHp = EnemyMaxHp = enemyMaxHealth;

            var passives = ComputePassiveTotals(loadout);
            var basicAttack = ComputeBasicAttackProfile(loadout, attributes, passives);
            _playerDefense = EstimatePlayerDefense(loadout);

            // A passive WeakenDamage/SlowAttackSpeed weapon actually changes the fight,
            // not just the feed badge's estimate of it.
            var effectiveEnemyDamage = Mathf.Max(1, Mathf.RoundToInt(enemyAttackDamage * passives.EnemyDamageMultiplier));
            var effectiveEnemySpeed = Mathf.Max(MinIntervalSeconds, enemyAttackSpeed * passives.EnemySpeedMultiplier);

            _timers = new List<AttackTimer>
            {
                new(CombatActor.Player, IntervalFor(basicAttack.Speed), basicAttack.Damage, basicAttack.IsRanged, null, 0f),
                new(CombatActor.Enemy, IntervalFor(effectiveEnemySpeed), effectiveEnemyDamage, false, null, 0f)
            };

            _abilityCastEffects = new Dictionary<string, StatusEffectData[]>();
            foreach (var (_, ability) in loadout.GetEquippedWeaponAbilityPairs())
            {
                if (ability.onCastEffects is { Length: > 0 })
                {
                    _abilityCastEffects[ability.abilityName] = ability.onCastEffects;
                }
            }

            foreach (var abilityProfile in ComputeAbilityProfiles(loadout, attributes, passives))
            {
                if (settings.ManualAbilityCasting)
                {
                    // Excluded from the auto-timer loop entirely — CombatPlaybackView
                    // drives this one in real time via AdvanceManualCooldowns/
                    // CastManualAbility instead, since its trigger moment depends on a
                    // live player tap rather than anything RunUntilDeathOrVictory can
                    // precompute in one batch.
                    _manualAbilities[abilityProfile.Name] = new ManualAbility(abilityProfile.Damage, abilityProfile.Cooldown);
                }
                else
                {
                    // Starts ON cooldown (NextFireTime = cooldown, not 0) — every other
                    // timer above starts ready, abilities deliberately don't.
                    _timers.Add(new AttackTimer(CombatActor.Player, abilityProfile.Cooldown, abilityProfile.Damage, true,
                        abilityProfile.Name, abilityProfile.Cooldown));
                }
            }

            // A passive DoT never expires and needs no trigger — just a permanent
            // ticking timer seeded alongside the basic-attack/enemy timers above.
            foreach (var effect in loadout.GetEquippedPassiveWeaponEffects())
            {
                if (effect.effectType == StatusEffectType.DamageOverTime)
                {
                    var tickInterval = Mathf.Max(MinIntervalSeconds, effect.tickIntervalSeconds);
                    _timers.Add(new AttackTimer(CombatActor.Player, tickInterval, Mathf.RoundToInt(effect.magnitude),
                        true, null, tickInterval, isDot: true));
                }
            }
        }

        // The player's basic-attack damage/speed from currently equipped weapon(s) (or
        // unarmed) — shared by the constructor and by EstimatePlayerDps below, so the
        // feed badge's difficulty estimate can never drift from what a fight actually does.
        private static WeaponAttackProfile ComputeBasicAttackProfile(PlayerLoadout loadout, PlayerAttributes attributes,
            PassiveTotals passives)
        {
            var leftHand = loadout.GetEquipped(EquipmentSlotType.LeftHand);
            var rightHand = loadout.GetEquipped(EquipmentSlotType.RightHand);
            var weapons = new List<ItemData>();
            if (leftHand?.category == ItemCategory.Weapon) weapons.Add(leftHand);
            if (rightHand?.category == ItemCategory.Weapon) weapons.Add(rightHand);

            var damage = weapons.Count > 0
                ? weapons.Sum(w => w.powerValue + ScalingBonus(w, attributes))
                : UnarmedAttackDamage;
            damage = Mathf.RoundToInt(damage * passives.SelfDamageMultiplier);

            var speed = (weapons.Count > 0 ? weapons.Average(w => w.attackSpeed) : UnarmedAttackSpeed)
                + attributes.Get(PlayerAttribute.Agility) * AgilityAttackSpeedPerPoint;
            speed *= passives.SelfSpeedMultiplier;

            var isRanged = weapons.Any(w => w.isRanged);

            return new WeaponAttackProfile(damage, speed, isRanged);
        }

        // Damage/cooldown for every ability on a currently-equipped weapon — shared by
        // the constructor (which routes each into the auto-timer list or the manual
        // dictionary) and by EstimatePlayerDps below.
        private static IEnumerable<AbilityProfile> ComputeAbilityProfiles(PlayerLoadout loadout, PlayerAttributes attributes,
            PassiveTotals passives)
        {
            var cooldownDivisor = 1f + attributes.Get(PlayerAttribute.Spirit) * SpiritCooldownDivisorPerPoint;
            foreach (var (weapon, ability) in loadout.GetEquippedWeaponAbilityPairs())
            {
                // An ability hits harder than a basic swing while scaling identically
                // off whatever the weapon scales off: the same base-power + attribute-scaling
                // math a basic attack uses, plus the ability's own bonus on top.
                var damage = Mathf.RoundToInt(
                    (weapon.powerValue + ScalingBonus(weapon, attributes) + ability.powerBonus) * passives.SelfDamageMultiplier);
                var cooldown = Mathf.Max(MinIntervalSeconds, ability.cooldownSeconds / cooldownDivisor);
                yield return new AbilityProfile(ability.abilityName, damage, cooldown);
            }
        }

        // Every equipped weapon's passiveEffects folded into one set of totals — reused
        // by the constructor (real fight), EstimatePlayerDps/EstimatePlayerDefense (feed
        // badge) and CombatResolver's enemy-side estimates, so passives can never drift
        // between "what the badge predicts" and "what the fight actually does."
        private static PassiveTotals ComputePassiveTotals(PlayerLoadout loadout)
        {
            var selfDamageBonus = 0f;
            var selfSpeedBonus = 0f;
            var selfArmor = 0f;
            var enemyDamageReduction = 0f;
            var enemySpeedPenalty = 0f;
            var enemyDotDps = 0f;

            foreach (var effect in loadout.GetEquippedPassiveWeaponEffects())
            {
                switch (effect.effectType)
                {
                    case StatusEffectType.EmpowerDamage:
                        selfDamageBonus += effect.magnitude;
                        break;
                    case StatusEffectType.HasteAttackSpeed:
                        selfSpeedBonus += effect.magnitude;
                        break;
                    case StatusEffectType.ArmorUp:
                        selfArmor += effect.magnitude;
                        break;
                    case StatusEffectType.WeakenDamage:
                        enemyDamageReduction += effect.magnitude;
                        break;
                    case StatusEffectType.SlowAttackSpeed:
                        enemySpeedPenalty += effect.magnitude;
                        break;
                    case StatusEffectType.DamageOverTime:
                        enemyDotDps += effect.magnitude / Mathf.Max(MinIntervalSeconds, effect.tickIntervalSeconds);
                        break;
                }
            }

            return new PassiveTotals(1f + selfDamageBonus, 1f + selfSpeedBonus, Mathf.RoundToInt(selfArmor),
                Mathf.Max(0f, 1f - enemyDamageReduction), 1f + enemySpeedPenalty, enemyDotDps);
        }

        // Steady-state DPS estimate with variance/misses ignored ("perfect rng") — used
        // by CombatResolver's feed-badge difficulty heuristic, not by the fight itself.
        // Basic attack plus every ability's damage/cooldown averaged into a rate, plus
        // any passive DoT's guaranteed tick rate. Deliberately doesn't average in the
        // uptime of temporary on-cast effects — see CombatResolver.
        public static float EstimatePlayerDps(PlayerLoadout loadout, PlayerAttributes attributes)
        {
            var passives = ComputePassiveTotals(loadout);
            var basicAttack = ComputeBasicAttackProfile(loadout, attributes, passives);
            var dps = basicAttack.Damage * basicAttack.Speed;

            foreach (var ability in ComputeAbilityProfiles(loadout, attributes, passives))
            {
                dps += ability.Damage / ability.Cooldown;
            }

            dps += passives.EnemyDotDps;

            return dps;
        }

        // Sum of every equipped non-weapon item's powerValue plus any passive ArmorUp —
        // the same computation the constructor uses for _playerDefense, exposed
        // statically so CombatResolver's difficulty heuristic can estimate incoming
        // enemy damage without building a full CombatSimulator instance.
        public static int EstimatePlayerDefense(PlayerLoadout loadout)
        {
            var defense = 0;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null && item.category != ItemCategory.Weapon)
                {
                    defense += item.powerValue;
                }
            }

            defense += ComputePassiveTotals(loadout).SelfArmor;
            return defense;
        }

        // Multiplier CombatResolver applies to the raw enemy attack stats before
        // estimating enemy DPS — mirrors what the constructor does to
        // enemyAttackDamage/enemyAttackSpeed for the real fight.
        public static float EstimateEnemyDamageMultiplier(PlayerLoadout loadout)
        {
            return ComputePassiveTotals(loadout).EnemyDamageMultiplier;
        }

        public static float EstimateEnemySpeedMultiplier(PlayerLoadout loadout)
        {
            return ComputePassiveTotals(loadout).EnemySpeedMultiplier;
        }

        // Constitution/Agility/Spirit's damage halves have no single global coefficient —
        // each weapon defines which attribute(s) it scales off and by how much, so a
        // weapon concept can scale off more than one (a magic sword off Constitution and
        // Spirit, a heavy bow off Agility and Constitution) and different tiers can scale
        // differently.
        private static int ScalingBonus(ItemData weapon, PlayerAttributes attributes)
        {
            if (weapon.attributeScalingEntries == null || weapon.attributeScalingEntries.Length == 0)
            {
                return 0;
            }

            var bonus = 0f;
            foreach (var entry in weapon.attributeScalingEntries)
            {
                bonus += attributes.Get(entry.attribute) * entry.coefficient;
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

                // A triggered (non-passive) DoT stops ticking once its buff window has
                // passed — drop it before picking the next beat rather than firing one
                // last tick past its expiry.
                _timers.RemoveAll(t => t.IsDot && t.NextFireTime > t.ExpiresAt);

                var actor = _timers[0];
                foreach (var timer in _timers)
                {
                    if (timer.NextFireTime < actor.NextFireTime)
                    {
                        actor = timer;
                    }
                }

                _currentTime = actor.NextFireTime;
                _activeEffects.RemoveAll(e => e.ExpiresAt <= _currentTime);

                if (actor.IsDot)
                {
                    beats.Add(ResolveDotTick(actor.BaseDamage));
                }
                else
                {
                    beats.Add(ResolveBeat(actor.Actor, actor.BaseDamage, actor.IsRanged, actor.AbilityName));
                    if (actor.Actor == CombatActor.Player && !string.IsNullOrEmpty(actor.AbilityName))
                    {
                        ApplyOnCastEffects(actor.AbilityName);
                    }
                }

                actor.NextFireTime += GetIntervalSeconds(actor);
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

        // Only the two basic-attack timers (no AbilityName, not a DoT) are affected by
        // Haste/Slow — ability cooldowns and DoT tick rates stay exactly as authored
        // (Spirit is still the only thing that moves an ability's cooldown), so
        // attack-speed effects can't have surprise interactions with cooldown pacing.
        private float GetIntervalSeconds(AttackTimer actor)
        {
            if (actor.IsDot || !string.IsNullOrEmpty(actor.AbilityName))
            {
                return actor.IntervalSeconds;
            }

            if (actor.Actor == CombatActor.Player)
            {
                return actor.IntervalSeconds / (1f + SumActiveEffect(StatusEffectType.HasteAttackSpeed));
            }

            return actor.IntervalSeconds * (1f + SumActiveEffect(StatusEffectType.SlowAttackSpeed));
        }

        private float SumActiveEffect(StatusEffectType type)
        {
            var sum = 0f;
            foreach (var effect in _activeEffects)
            {
                if (effect.Type == type)
                {
                    sum += effect.Magnitude;
                }
            }

            return sum;
        }

        // A cast ability's onCastEffects, applied right after its own hit resolves —
        // shared by the auto-timer loop above and a manual tap-cast below. A
        // DamageOverTime effect becomes a temporary ticking timer (AbilityName left
        // null, same as a basic attack, so it never collides with a real ability's
        // cooldown-bar widget in CombatPlaybackView); everything else becomes a timed
        // entry in _activeEffects.
        private void ApplyOnCastEffects(string abilityName)
        {
            if (!_abilityCastEffects.TryGetValue(abilityName, out var effects))
            {
                return;
            }

            foreach (var effect in effects)
            {
                if (effect.effectType == StatusEffectType.DamageOverTime)
                {
                    var tickInterval = Mathf.Max(MinIntervalSeconds, effect.tickIntervalSeconds);
                    _timers.Add(new AttackTimer(CombatActor.Player, tickInterval, Mathf.RoundToInt(effect.magnitude),
                        true, null, _currentTime + tickInterval, isDot: true,
                        expiresAt: _currentTime + effect.durationSeconds));
                }
                else
                {
                    _activeEffects.Add(new TimedEffect(effect.effectType, effect.magnitude,
                        _currentTime + effect.durationSeconds));
                }
            }
        }

        // Shared miss/variance/damage-application math — used by the auto-timer beat
        // loop above and by a manual ability's on-tap cast below, so both play out
        // identically. Currently-active EmpowerDamage/WeakenDamage/ArmorUp effects are
        // folded in here so both paths honor them the same way.
        private CombatBeat ResolveBeat(CombatActor actor, int baseDamage, bool isRanged, string abilityName)
        {
            if (_rng.NextDouble() < MissChance)
            {
                return new CombatBeat(actor, isRanged, true, 0, abilityName);
            }

            var variance = 1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * DamageVariance;
            var multiplier = actor == CombatActor.Player
                ? 1f + SumActiveEffect(StatusEffectType.EmpowerDamage)
                : Mathf.Max(0f, 1f - SumActiveEffect(StatusEffectType.WeakenDamage));
            var raw = Mathf.RoundToInt(baseDamage * variance * multiplier);

            int applied;
            if (actor == CombatActor.Player)
            {
                applied = Mathf.Max(1, raw);
                EnemyHp = Mathf.Max(0, EnemyHp - applied);
            }
            else
            {
                var defense = _playerDefense + Mathf.RoundToInt(SumActiveEffect(StatusEffectType.ArmorUp));
                applied = Mathf.Max(1, raw - defense);
                PlayerHp = Mathf.Max(0, PlayerHp - applied);
            }

            return new CombatBeat(actor, isRanged, false, applied, abilityName);
        }

        // A DoT tick already "landed" when its debuff was applied — no further miss
        // chance, variance, or defense to resolve, just flat damage. AbilityName is
        // always null on the resulting beat (see ApplyOnCastEffects); IsDotTick is set
        // instead so CombatPlaybackView can still render it distinctly from a plain
        // basic-attack beat.
        private CombatBeat ResolveDotTick(int magnitude)
        {
            var applied = Mathf.Max(1, magnitude);
            EnemyHp = Mathf.Max(0, EnemyHp - applied);
            return new CombatBeat(CombatActor.Player, true, false, applied, null, isDotTick: true);
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
            var beat = ResolveBeat(CombatActor.Player, ability.Damage, true, abilityName);
            ApplyOnCastEffects(abilityName);
            return beat;
        }

        private static float IntervalFor(float attacksPerSecond)
        {
            return 1f / Mathf.Max(MinIntervalSeconds, attacksPerSecond);
        }

        private readonly struct WeaponAttackProfile
        {
            public readonly int Damage;
            public readonly float Speed;
            public readonly bool IsRanged;

            public WeaponAttackProfile(int damage, float speed, bool isRanged)
            {
                Damage = damage;
                Speed = speed;
                IsRanged = isRanged;
            }
        }

        private readonly struct AbilityProfile
        {
            public readonly string Name;
            public readonly int Damage;
            public readonly float Cooldown;

            public AbilityProfile(string name, int damage, float cooldown)
            {
                Name = name;
                Damage = damage;
                Cooldown = cooldown;
            }
        }

        // Every equipped weapon's passiveEffects folded into scalar totals — see
        // ComputePassiveTotals.
        private readonly struct PassiveTotals
        {
            public readonly float SelfDamageMultiplier;
            public readonly float SelfSpeedMultiplier;
            public readonly int SelfArmor;
            public readonly float EnemyDamageMultiplier;
            public readonly float EnemySpeedMultiplier;
            public readonly float EnemyDotDps;

            public PassiveTotals(float selfDamageMultiplier, float selfSpeedMultiplier, int selfArmor,
                float enemyDamageMultiplier, float enemySpeedMultiplier, float enemyDotDps)
            {
                SelfDamageMultiplier = selfDamageMultiplier;
                SelfSpeedMultiplier = selfSpeedMultiplier;
                SelfArmor = selfArmor;
                EnemyDamageMultiplier = enemyDamageMultiplier;
                EnemySpeedMultiplier = enemySpeedMultiplier;
                EnemyDotDps = enemyDotDps;
            }
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

        // A currently-active, time-limited buff/debuff from an ability's onCastEffects
        // (everything except DamageOverTime, which is modeled as its own AttackTimer
        // instead — see ApplyOnCastEffects).
        private sealed class TimedEffect
        {
            public readonly StatusEffectType Type;
            public readonly float Magnitude;
            public readonly float ExpiresAt;

            public TimedEffect(StatusEffectType type, float magnitude, float expiresAt)
            {
                Type = type;
                Magnitude = magnitude;
                ExpiresAt = expiresAt;
            }
        }

        private sealed class AttackTimer
        {
            public readonly CombatActor Actor;
            public readonly float IntervalSeconds;
            public readonly int BaseDamage;
            public readonly bool IsRanged;
            public readonly string AbilityName;
            public readonly bool IsDot;
            public readonly float ExpiresAt;
            public float NextFireTime;

            public AttackTimer(CombatActor actor, float intervalSeconds, int baseDamage, bool isRanged,
                string abilityName, float initialDelay, bool isDot = false, float expiresAt = float.PositiveInfinity)
            {
                Actor = actor;
                IntervalSeconds = intervalSeconds;
                BaseDamage = baseDamage;
                IsRanged = isRanged;
                AbilityName = abilityName;
                NextFireTime = initialDelay;
                IsDot = isDot;
                ExpiresAt = expiresAt;
            }
        }
    }
}
