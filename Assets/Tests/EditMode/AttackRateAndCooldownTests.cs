using System.Linq;
using DoomScroller.Combat;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    // "They attack at the correct rates" / "abilities are being used off cooldown" —
    // timer *scheduling* in CombatSimulator never touches rng (only per-hit damage
    // variance/miss does), so every test here predicts an exact beat count/order via
    // TestFixtures.MergeCounts, a reference model of the same earliest-time-wins,
    // first-declared-wins-ties selection RunUntilDeathOrVictory uses on its own
    // _timers list, instead of a statistical approximation.
    public class AttackRateAndCooldownTests
    {
        // Mirrors CombatSimulator's own private consts — there's no public surface for
        // them, so these are pinned here deliberately: if either constant changes, the
        // corresponding test below should fail and call it out, not silently drift.
        private const float AgilityAttackSpeedPerPoint = 0.015f;
        private const float SpiritCooldownDivisorPerPoint = 0.03f;

        [Test]
        public void AttackRate_PlayerAndEnemyBeatCounts_MatchExactSchedulingModel()
        {
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 2f);
            var loadout = TestFixtures.MakeLoadout(weapon);
            const float enemySpeed = 1f;

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), new GameSettings(), 100000, 100000,
                enemyAttackDamage: 1, enemyAttackSpeed: enemySpeed, enemyWeaknesses: null, rng: new FixedRandom());
            var beats = sim.RunUntilDeathOrVictory();

            var playerInterval = 1f / (2f + PlayerAttributes.BaseValue * AgilityAttackSpeedPerPoint);
            var (expectedPlayer, expectedEnemy) = TestFixtures.MergeCounts(playerInterval, 0f, 1f / enemySpeed, 0f, 200);

            Assert.AreEqual(200, beats.Count, "neither side should die within 200 beats given the huge HP pools");
            Assert.AreEqual(expectedPlayer, beats.Count(b => b.Actor == CombatActor.Player));
            Assert.AreEqual(expectedEnemy, beats.Count(b => b.Actor == CombatActor.Enemy));
        }

        [Test]
        public void Agility_ShortensPlayerAttackInterval_ByExactlyItsPerPointFormula()
        {
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f,
                scaling: new[] { new AttributeScaling { attribute = PlayerAttribute.Constitution, coefficient = 0f } });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var baseAttributes = new PlayerAttributes();
            var boostedAttributes = new PlayerAttributes();
            boostedAttributes.GrantPoints(10);
            for (var i = 0; i < 10; i++)
            {
                boostedAttributes.Allocate(PlayerAttribute.Agility);
            }

            var baseSpeed = CombatSimulator.EstimatePlayerDps(loadout, baseAttributes) / 10f;
            var boostedSpeed = CombatSimulator.EstimatePlayerDps(loadout, boostedAttributes) / 10f;

            Assert.AreEqual(10 * AgilityAttackSpeedPerPoint, boostedSpeed - baseSpeed, 0.0001f);
        }

        [Test]
        public void ManualAbility_RefusesToCast_UntilItsCooldownFullyElapses()
        {
            var ability = new WeaponAbilityData { abilityName = "Test", powerBonus = 6, cooldownSeconds = 3f };
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f, abilities: new[] { ability });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000, 100000, 0, 0.0001f, null,
                new FixedRandom());

            Assert.IsNull(sim.CastManualAbility("Test"), "every ability starts on cooldown when a fight begins");

            // The Spirit divisor (baseline Spirit=5) already shrinks the manual-mode
            // cooldown too, same as the auto-timer path — 3s authored becomes ~2.6s
            // effective, so advance relative to that instead of the raw 3s.
            var effectiveCooldown = 3f / (1f + PlayerAttributes.BaseValue * SpiritCooldownDivisorPerPoint);

            sim.AdvanceManualCooldowns(effectiveCooldown * 0.5f);
            Assert.IsNull(sim.CastManualAbility("Test"));
            Assert.IsFalse(sim.GetManualAbilityStates().Single().IsReady);

            sim.AdvanceManualCooldowns(effectiveCooldown);
            Assert.IsTrue(sim.GetManualAbilityStates().Single().IsReady);

            var beat = sim.CastManualAbility("Test");
            Assert.IsNotNull(beat);
            Assert.AreEqual("Test", beat.Value.AbilityName);
            // (10 + 0 scaling + 6 powerBonus) * 1x type multiplier * 1x self multiplier.
            Assert.AreEqual(16, beat.Value.DisplayDamage);

            Assert.IsNull(sim.CastManualAbility("Test"), "casting should re-arm the cooldown immediately");
        }

        [Test]
        public void AutoTimerAbility_FiresExactlyOnCooldownSchedule_AndSpiritShortensIt()
        {
            const float weaponSpeed = 1f;
            const float enemySpeed = 1f;
            const float cooldown = 2f;
            var playerInterval = 1f / (weaponSpeed + PlayerAttributes.BaseValue * AgilityAttackSpeedPerPoint);

            (int PlayerCount, int EnemyCount, int AbilityCount) RunWithSpirit(int spiritPoints)
            {
                var ability = new WeaponAbilityData { abilityName = "Test", powerBonus = 1, cooldownSeconds = cooldown };
                var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: weaponSpeed, abilities: new[] { ability });
                var loadout = TestFixtures.MakeLoadout(weapon);

                var attributes = new PlayerAttributes();
                if (spiritPoints > 0)
                {
                    attributes.GrantPoints(spiritPoints);
                    for (var i = 0; i < spiritPoints; i++)
                    {
                        attributes.Allocate(PlayerAttribute.Spirit);
                    }
                }

                var sim = new CombatSimulator(loadout, attributes, new GameSettings(), 1000000, 1000000,
                    enemyAttackDamage: 0, enemyAttackSpeed: enemySpeed, enemyWeaknesses: null, rng: new FixedRandom());
                var beats = sim.RunUntilDeathOrVictory();

                return (beats.Count(b => b.Actor == CombatActor.Player && b.AbilityName == null),
                    beats.Count(b => b.Actor == CombatActor.Enemy),
                    beats.Count(b => b.AbilityName == "Test"));
            }

            // A full three-way reference merge (player basic-attack timer, enemy timer,
            // ability timer) — all three genuinely compete for the same 200 beats, so
            // unlike the attack-rate test there's no way to isolate just two of them.
            (int expectedPlayer, int expectedEnemy, int expectedAbility) ExpectedFor(int spiritPoints)
            {
                var divisor = 1f + (PlayerAttributes.BaseValue + spiritPoints) * SpiritCooldownDivisorPerPoint;
                var abilityInterval = cooldown / divisor;
                return TestFixtures.MergeCounts3(playerInterval, 0f, 1f / enemySpeed, 0f, abilityInterval,
                    abilityInterval, 200);
            }

            var baseline = RunWithSpirit(0);
            var (baselinePlayer, baselineEnemy, baselineAbility) = ExpectedFor(0);
            Assert.AreEqual(baselinePlayer, baseline.PlayerCount);
            Assert.AreEqual(baselineEnemy, baseline.EnemyCount);
            Assert.AreEqual(baselineAbility, baseline.AbilityCount);

            var spirited = RunWithSpirit(10);
            var (spiritedPlayer, spiritedEnemy, spiritedAbility) = ExpectedFor(10);
            Assert.AreEqual(spiritedPlayer, spirited.PlayerCount);
            Assert.AreEqual(spiritedEnemy, spirited.EnemyCount);
            Assert.AreEqual(spiritedAbility, spirited.AbilityCount);

            Assert.Greater(spirited.AbilityCount, baseline.AbilityCount,
                "more Spirit should shorten the cooldown and land strictly more casts in the same beat budget");
        }

        [Test]
        public void HasteAttackSpeed_HalvesPlayerInterval_WhenActiveFromTheStartOfTheFight()
        {
            var haste = new WeaponAbilityData
            {
                abilityName = "Haste", cooldownSeconds = 1f,
                onCastEffects = new[] { new StatusEffectData { effectType = StatusEffectType.HasteAttackSpeed, magnitude = 1f, durationSeconds = 1000f } }
            };
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 2f, abilities: new[] { haste });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);
            const float enemySpeed = 1f;

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000000, 1000000, 0, enemySpeed,
                null, new FixedRandom());
            // Every manual ability starts on cooldown — clear it before the real cast.
            sim.AdvanceManualCooldowns(9999f);
            sim.CastManualAbility("Haste");
            var beats = sim.RunUntilDeathOrVictory();

            var basePlayerInterval = 1f / (2f + PlayerAttributes.BaseValue * AgilityAttackSpeedPerPoint);
            var hastedInterval = basePlayerInterval / 2f;
            var (expectedPlayer, expectedEnemy) = TestFixtures.MergeCounts(hastedInterval, 0f, 1f / enemySpeed, 0f, 200);

            Assert.AreEqual(expectedPlayer, beats.Count(b => b.Actor == CombatActor.Player && b.AbilityName == null));
            Assert.AreEqual(expectedEnemy, beats.Count(b => b.Actor == CombatActor.Enemy));

            var (unhastedPlayer, _) = TestFixtures.MergeCounts(basePlayerInterval, 0f, 1f / enemySpeed, 0f, 200);
            Assert.Greater(expectedPlayer, unhastedPlayer, "halving the interval should land strictly more swings");
        }

        [Test]
        public void SlowAttackSpeed_DoublesEnemyInterval_WhenActiveFromTheStartOfTheFight()
        {
            var slow = new WeaponAbilityData
            {
                abilityName = "Slow", cooldownSeconds = 1f,
                onCastEffects = new[] { new StatusEffectData { effectType = StatusEffectType.SlowAttackSpeed, magnitude = 1f, durationSeconds = 1000f } }
            };
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 2f, abilities: new[] { slow });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);
            const float enemySpeed = 1f;

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000000, 1000000, 0, enemySpeed,
                null, new FixedRandom());
            sim.AdvanceManualCooldowns(9999f);
            sim.CastManualAbility("Slow");
            var beats = sim.RunUntilDeathOrVictory();

            var playerInterval = 1f / (2f + PlayerAttributes.BaseValue * AgilityAttackSpeedPerPoint);
            var slowedEnemyInterval = (1f / enemySpeed) * 2f;
            var (expectedPlayer, expectedEnemy) = TestFixtures.MergeCounts(playerInterval, 0f, slowedEnemyInterval, 0f, 200);

            Assert.AreEqual(expectedPlayer, beats.Count(b => b.Actor == CombatActor.Player && b.AbilityName == null));
            Assert.AreEqual(expectedEnemy, beats.Count(b => b.Actor == CombatActor.Enemy));

            var (_, unslowedEnemy) = TestFixtures.MergeCounts(playerInterval, 0f, 1f / enemySpeed, 0f, 200);
            Assert.Greater(unslowedEnemy, expectedEnemy, "doubling the enemy's interval should land strictly fewer hits");
        }
    }
}
