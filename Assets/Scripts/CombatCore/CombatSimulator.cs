using System;
using System.Collections.Generic;

namespace DoomScroller.CombatCore
{
    // A resumable fight simulation over plain data. Nothing here touches the game's
    // player state; PlayerHp/PlayerStartHp are this fight's own ephemeral instance.
    // Deterministic for a given FightInput + System.Random, and free of anything
    // platform-dependent (no dictionary iteration order in the loop), so the server
    // and the device resolve the same input to the same beats.
    public class CombatSimulator
    {
        // Small per-swing randomness — a flat percentage damage variance and a flat
        // miss chance, independent of stats. Both are early tuning knobs.
        private const float DamageVariance = 0.15f;
        private const float MissChance = 0.08f;

        // Guards against a degenerate fight (e.g. authored 0-damage/0-speed content)
        // looping forever — a *global* budget across the whole fight, not reset by the
        // Revive. If neither side has died by the time it's exhausted, whoever has the
        // healthier remaining fraction is forced to win.
        private const int MaxBeats = 200;

        private readonly List<AttackTimer> _timers;
        private readonly Dictionary<string, EffectInput[]> _abilityCastEffects;
        private readonly List<TimedEffect> _activeEffects = new List<TimedEffect>();
        private readonly int _playerDefense;
        private readonly System.Random _rng;
        private readonly bool _reviveArmed;
        private readonly int _revivePercent;
        private int _beatsRun;
        private float _currentTime;
        private bool _reviveUsed;

        public int PlayerHp { get; private set; }
        public int PlayerStartHp { get; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; }

        public bool IsPlayerDead => PlayerHp <= 0;
        public bool IsEnemyDead => EnemyHp <= 0;

        // Whether the armed Revive fired, and the index (into the beats returned by
        // RunUntilDeathOrVictory) of the beat that killed the player just before it did.
        public bool ReviveFired => _reviveUsed;
        public int ReviveBeatIndex { get; private set; } = -1;

        // The HP the Revive restored (0 until it fires).
        public int ReviveHp { get; private set; }

        // Cumulative damage dealt to the enemy so far — always accurate even when the
        // enemy can never actually reach 0 (the resource-node minigame's
        // effectively-infinite-health construction).
        public int DamageDealt => EnemyMaxHp - EnemyHp;

        // Resolves a whole fight in one call.
        public static FightResult Resolve(FightInput input)
        {
            var sim = new CombatSimulator(input);
            var beats = sim.RunUntilDeathOrVictory();
            return new FightResult
            {
                outcome = sim.IsEnemyDead ? FightOutcome.Win : FightOutcome.Loss,
                beats = beats.ToArray(),
                reviveFired = sim.ReviveFired,
                reviveBeatIndex = sim.ReviveBeatIndex,
                finalPlayerHp = sim.PlayerHp,
                finalEnemyHp = sim.EnemyHp
            };
        }

        // rng is injectable so tests (and the badge's own seed stream) can drive it;
        // by default the fight draws from input.seed.
        public CombatSimulator(FightInput input, System.Random rng = null)
        {
            _rng = rng ?? new System.Random(input.seed);
            var player = input.player;
            var enemy = input.enemy;
            _reviveArmed = input.reviveArmed;
            _revivePercent = input.revivePercent;
            PlayerHp = PlayerStartHp = Math.Max(1, player.startHp);
            EnemyHp = EnemyMaxHp = enemy.maxHp;

            var passives = CombatStats.ComputePassiveTotals(player);
            var basicAttack = CombatStats.ComputeBasicAttackProfile(player, passives, enemy.damageMultipliers);
            _playerDefense = CombatStats.EstimatePlayerDefense(player);

            // A passive WeakenDamage/SlowAttackSpeed weapon actually changes the fight,
            // not just the feed badge's estimate of it.
            var effectiveEnemyDamage = Math.Max(1,
                CombatStats.RoundToInt(enemy.attackDamage * passives.EnemyDamageMultiplier));
            var effectiveEnemySpeed = Math.Max(CombatStats.MinIntervalSeconds,
                enemy.attackSpeed * passives.EnemySpeedMultiplier);

            _timers = new List<AttackTimer>
            {
                new AttackTimer(CombatActor.Player, IntervalFor(basicAttack.Speed), basicAttack.Damage, basicAttack.IsRanged, null, 0f)
            };

            // A resource node never fights back — omitting its timer entirely (rather
            // than e.g. zeroing its damage) is the only clean way to make it truly
            // passive: 0 damage still floors at 1 applied per beat, and 0 speed clamps
            // to 20/sec via MinIntervalSeconds.
            if (enemy.canAttack)
            {
                _timers.Add(new AttackTimer(CombatActor.Enemy, IntervalFor(effectiveEnemySpeed), effectiveEnemyDamage, false, null, 0f));
            }

            _abilityCastEffects = new Dictionary<string, EffectInput[]>();
            if (player.abilities != null)
            {
                foreach (var ability in player.abilities)
                {
                    if (ability.onCastEffects != null && ability.onCastEffects.Length > 0)
                    {
                        _abilityCastEffects[ability.name] = ability.onCastEffects;
                    }
                }
            }

            foreach (var abilityProfile in CombatStats.ComputeAbilityProfiles(player, passives, enemy.damageMultipliers))
            {
                // Starts ON cooldown (NextFireTime = cooldown, not 0) — every other
                // timer above starts ready, abilities deliberately don't.
                _timers.Add(new AttackTimer(CombatActor.Player, abilityProfile.Cooldown, abilityProfile.Damage, true,
                    abilityProfile.Name, abilityProfile.Cooldown));
            }

            // A passive DoT never expires and needs no trigger — just a permanent
            // ticking timer seeded alongside the basic-attack/enemy timers above.
            if (player.passives != null)
            {
                foreach (var effect in player.passives)
                {
                    if (effect.effectType == StatusEffectType.DamageOverTime)
                    {
                        var tickInterval = Math.Max(CombatStats.MinIntervalSeconds, effect.tickIntervalSeconds);
                        _timers.Add(new AttackTimer(CombatActor.Player, tickInterval, CombatStats.RoundToInt(effect.magnitude),
                            true, null, tickInterval, isDot: true));
                    }
                }
            }
        }

        // Runs beats until someone's dead, or this instance's whole MaxBeats budget is
        // exhausted — in which case the weaker side is forced to 0 so the caller only
        // ever has to check IsPlayerDead/IsEnemyDead, never a third "nobody died" state.
        // An armed Revive fires automatically (once) the first time the player dies, and
        // the same fight carries on from there.
        public List<CombatBeat> RunUntilDeathOrVictory()
        {
            var beats = new List<CombatBeat>();
            RunBatch(beats);

            if (PlayerHp <= 0 && _reviveArmed && !_reviveUsed)
            {
                _reviveUsed = true;
                ReviveBeatIndex = beats.Count - 1;
                ReviveHp = Math.Max(1, (Math.Max(0, PlayerStartHp) * _revivePercent + 99) / 100);
                PlayerHp = ReviveHp;
                RunBatch(beats);
            }

            return beats;
        }

        private void RunBatch(List<CombatBeat> beats)
        {
            while (_beatsRun < MaxBeats && PlayerHp > 0 && EnemyHp > 0)
            {
                _beatsRun++;
                RemoveExpiredDots();

                var actor = NextTimer();
                _currentTime = actor.NextFireTime;
                _activeEffects.RemoveAll(e => e.ExpiresAt <= _currentTime);
                FireTimer(actor, beats);
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
        }

        // The resource-node minigame's entry point — same beat-picking loop, but bounded
        // by elapsed virtual time instead of death (there's no enemy timer when
        // canAttack was false, so every returned beat is CombatActor.Player). MaxBeats
        // still applies as a safety net. Stops the instant the next-due timer would fire
        // past `seconds` rather than resolving it and stopping after — so DamageDealt
        // only ever reflects beats that actually landed within the window.
        public List<CombatBeat> RunForDuration(float seconds)
        {
            var beats = new List<CombatBeat>();

            while (_beatsRun < MaxBeats)
            {
                RemoveExpiredDots();

                var actor = NextTimer();
                if (actor.NextFireTime > seconds)
                {
                    break;
                }

                _beatsRun++;
                _currentTime = actor.NextFireTime;
                _activeEffects.RemoveAll(e => e.ExpiresAt <= _currentTime);
                FireTimer(actor, beats);
            }

            return beats;
        }

        // A triggered (non-passive) DoT stops ticking once its buff window has passed —
        // drop it before picking the next beat rather than firing one last tick past
        // its expiry.
        private void RemoveExpiredDots()
        {
            _timers.RemoveAll(t => t.IsDot && t.NextFireTime > t.ExpiresAt);
        }

        // Earliest NextFireTime wins; first-declared wins ties.
        private AttackTimer NextTimer()
        {
            var actor = _timers[0];
            foreach (var timer in _timers)
            {
                if (timer.NextFireTime < actor.NextFireTime)
                {
                    actor = timer;
                }
            }

            return actor;
        }

        private void FireTimer(AttackTimer actor, List<CombatBeat> beats)
        {
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

        // A cast ability's onCastEffects, applied right after its own hit resolves. A
        // DamageOverTime effect becomes a temporary ticking timer (AbilityName left
        // null, same as a basic attack, so it never collides with a real ability's
        // cooldown-bar widget); everything else becomes a timed entry in _activeEffects.
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
                    var tickInterval = Math.Max(CombatStats.MinIntervalSeconds, effect.tickIntervalSeconds);
                    _timers.Add(new AttackTimer(CombatActor.Player, tickInterval, CombatStats.RoundToInt(effect.magnitude),
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

        // Miss/variance/damage-application math. Currently-active EmpowerDamage/
        // WeakenDamage/ArmorUp effects are folded in here.
        private CombatBeat ResolveBeat(CombatActor actor, int baseDamage, bool isRanged, string abilityName)
        {
            if (_rng.NextDouble() < MissChance)
            {
                return new CombatBeat(actor, isRanged, true, 0, abilityName);
            }

            var variance = 1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * DamageVariance;
            var multiplier = actor == CombatActor.Player
                ? 1f + SumActiveEffect(StatusEffectType.EmpowerDamage)
                : Math.Max(0f, 1f - SumActiveEffect(StatusEffectType.WeakenDamage));
            var raw = CombatStats.RoundToInt(baseDamage * variance * multiplier);

            int applied;
            if (actor == CombatActor.Player)
            {
                applied = Math.Max(1, raw);
                EnemyHp = Math.Max(0, EnemyHp - applied);
            }
            else
            {
                var defense = _playerDefense + CombatStats.RoundToInt(SumActiveEffect(StatusEffectType.ArmorUp));
                applied = Math.Max(1, raw - defense);
                PlayerHp = Math.Max(0, PlayerHp - applied);
            }

            return new CombatBeat(actor, isRanged, false, applied, abilityName);
        }

        // A DoT tick already "landed" when its debuff was applied — no further miss
        // chance, variance, or defense to resolve, just flat damage. AbilityName is
        // always null on the resulting beat; IsDotTick is set instead.
        private CombatBeat ResolveDotTick(int magnitude)
        {
            var applied = Math.Max(1, magnitude);
            EnemyHp = Math.Max(0, EnemyHp - applied);
            return new CombatBeat(CombatActor.Player, true, false, applied, null, isDotTick: true);
        }

        private static float IntervalFor(float attacksPerSecond)
        {
            return 1f / Math.Max(CombatStats.MinIntervalSeconds, attacksPerSecond);
        }

        // A currently-active, time-limited buff/debuff from an ability's onCastEffects
        // (everything except DamageOverTime, which is modeled as its own AttackTimer).
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
