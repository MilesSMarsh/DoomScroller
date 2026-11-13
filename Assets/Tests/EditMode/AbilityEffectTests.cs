using System.Linq;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    // "The abilities are doing their intended function" / "the abilities are doing the
    // right amount of damage" — there's no authored ability catalog left to name-check
    // against (the whole point of the wipe), so these exercise the on-cast/passive
    // *effect system* itself generically: StatusEffectType handling, expiry, and DoT
    // ticking, using hand-built WeaponAbilityData/StatusEffectData fixtures.
    public class AbilityEffectTests
    {
        [Test]
        public void EmpowerDamage_OnCastEffect_AppliesFromTheNextCastAndExpiresOnSchedule()
        {
            // cooldown(2) < duration(3) < 2*cooldown(4): every cast after the first sees
            // exactly one still-active EmpowerDamage buff from the previous cast (the
            // one before that has always just expired) — a steady state that only holds
            // if expiry is actually pruning on schedule, not just "never expiring" or
            // "expiring instantly".
            var hit = new WeaponAbilityData
            {
                abilityName = "Hit", powerBonus = 6, cooldownSeconds = 2f,
                onCastEffects = new[]
                {
                    new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.5f, durationSeconds = 3f }
                }
            };
            // Attack speed near zero -> floored by the baseline Agility bonus to ~13.3s/swing
            // (see CombatSimulator's AgilityAttackSpeedPerPoint), safely outside this
            // test's ~9s observation window, so the player's own basic attack can't
            // intrude on the ability-only sequence being asserted below.
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 0.0000001f, abilities: new[] { hit });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000000, 1000000,
                enemyAttackDamage: 0, enemyAttackSpeed: 0.0000001f, enemyWeaknesses: null, rng: new FixedRandom());
            var beats = sim.RunUntilDeathOrVictory();

            // beats[0] = player's single lifetime basic-attack fire, beats[1] = enemy's
            // single lifetime fire (both lose the opening tie to nobody but each other;
            // player wins), beats[2..] = "Hit" repeating alone from then on.
            var abilityBeats = beats.Skip(2).Take(5).ToList();
            Assert.IsTrue(abilityBeats.All(b => b.AbilityName == "Hit"));

            Assert.AreEqual(16, abilityBeats[0].DisplayDamage, "first cast: no buff active yet, (10+6)*1");
            Assert.AreEqual(24, abilityBeats[1].DisplayDamage, "second cast: previous cast's buff still active, (10+6)*1.5");
            Assert.AreEqual(24, abilityBeats[2].DisplayDamage);
            Assert.AreEqual(24, abilityBeats[3].DisplayDamage);
            Assert.AreEqual(24, abilityBeats[4].DisplayDamage);
        }

        [Test]
        public void ArmorUp_OnCastEffect_ReducesTheNextIncomingHit_ByExactlyItsMagnitude()
        {
            var shield = new WeaponAbilityData
            {
                abilityName = "Shield", cooldownSeconds = 1f,
                onCastEffects = new[]
                {
                    new StatusEffectData { effectType = StatusEffectType.ArmorUp, magnitude = 8f, durationSeconds = 1000f }
                }
            };
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 1f, abilities: new[] { shield });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, 100000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            // Shield auto-casts at ~0.87s; the enemy's t=0 hit lands before it, the t=1 hit after.
            var beats = sim.RunUntilDeathOrVictory();
            var enemyBeat = beats.SkipWhile(b => b.AbilityName != "Shield").First(b => b.Actor == CombatActor.Enemy);

            Assert.AreEqual(12, enemyBeat.DisplayDamage, "20 raw - 8 ArmorUp");
        }

        [Test]
        public void WeakenDamage_OnCastEffect_ReducesTheEnemysNextHit_ByExactlyItsFraction()
        {
            var weaken = new WeaponAbilityData
            {
                abilityName = "Weaken", cooldownSeconds = 1f,
                onCastEffects = new[]
                {
                    new StatusEffectData { effectType = StatusEffectType.WeakenDamage, magnitude = 0.25f, durationSeconds = 1000f }
                }
            };
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 1f, abilities: new[] { weaken });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, 100000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            // Weaken auto-casts at ~0.87s; only enemy hits after that are weakened.
            var beats = sim.RunUntilDeathOrVictory();
            var enemyBeat = beats.SkipWhile(b => b.AbilityName != "Weaken").First(b => b.Actor == CombatActor.Enemy);

            Assert.AreEqual(15, enemyBeat.DisplayDamage, "20 raw * (1 - 0.25), no armor to further reduce it");
        }

        [Test]
        public void DamageOverTime_OnCastEffect_TicksOnScheduleThenStopsAtExpiry()
        {
            var poison = new WeaponAbilityData
            {
                abilityName = "Poison", cooldownSeconds = 10f,
                onCastEffects = new[]
                {
                    new StatusEffectData { effectType = StatusEffectType.DamageOverTime, magnitude = 4f, tickIntervalSeconds = 1f, durationSeconds = 3.5f }
                }
            };
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 0.0000001f, abilities: new[] { poison });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000000, 1000000,
                enemyAttackDamage: 0, enemyAttackSpeed: 0.0000001f, enemyWeaknesses: null, rng: new FixedRandom());
            // 10s authored cooldown -> first cast at ~8.7s (Spirit divisor 1.15); its DoT
            // ticks at +1,+2,+3s and a 4th at +4s would exceed the 3.5s duration, so it's
            // pruned. The next cast is at ~17.4s, past the 16s window.
            var beats = sim.RunForDuration(16f);

            var ticks = beats.Where(b => b.IsDotTick).ToList();
            Assert.AreEqual(3, ticks.Count);
            Assert.IsTrue(ticks.All(t => t.DisplayDamage == 4 && t.AbilityName == null));
        }

        [Test]
        public void PassiveDamageOverTime_TicksForTheWholeFight_WithNoExpiry()
        {
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 0.0000001f,
                passives: new[] { new StatusEffectData { effectType = StatusEffectType.DamageOverTime, magnitude = 3f, tickIntervalSeconds = 1f } });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, 10,
                enemyAttackDamage: 0, enemyAttackSpeed: 0.0000001f, enemyWeaknesses: null, rng: new FixedRandom());
            var beats = sim.RunUntilDeathOrVictory();

            var ticks = beats.Where(b => b.IsDotTick).ToList();
            // Enemy starts at 10 HP: the player's one guaranteed (floor-of-1) basic
            // attack takes it to 9, then three ticks of 3 finish it off exactly at 0.
            Assert.AreEqual(3, ticks.Count);
            Assert.IsTrue(ticks.All(t => t.DisplayDamage == 3));
            Assert.IsTrue(sim.IsEnemyDead);
            Assert.AreEqual(0, sim.EnemyHp);
        }
    }
}
