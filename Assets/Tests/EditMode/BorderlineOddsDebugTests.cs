using System;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Covers task 023 criteria that don't need the Editor UI: the finder's answer checked
    // against a literal brute-force reading of the spec, the penalty arithmetic, and the
    // PlayerHealth debug setter's save/heal/regen behaviour.
    public class BorderlineOddsDebugTests
    {
        private const int Seed = 7;
        private const int TrueMax = 100;
        private const int RegenPerTick = 10;
        private const float RegenSeconds = 30f;

        private static EncounterData MakeEncounter(int hp, int dmg, float speed)
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.type = EncounterType.Enemy;
            data.enemyHealth = hp;
            data.enemyAttackDamage = dmg;
            data.enemyAttackSpeed = speed;
            return data;
        }

        private static PlayerLoadout Loadout(int power) =>
            TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: power, attackSpeed: 1f));

        private static WinOddsTier Tier(PlayerLoadout l, int hp, EncounterData d) =>
            WinOddsEstimator.Estimate(l, new PlayerAttributes(), hp, d, Seed).Tier;

        // Same arithmetic as the finder, but via PlayerHealth.ApplyDefeatPenalty.
        private static int PenaltyFrom(int trueMax, int currentMax)
        {
            var h = new PlayerHealth(trueMax, 0, 1f);
            h.LoadState(trueMax, currentMax, DateTime.UtcNow.Ticks);
            h.ApplyDefeatPenalty();
            return h.CurrentMaxHealth;
        }

        // Literal reading of the spec: the lowest HP in [Floor, TrueMax] whose tier equals
        // the true-max tier AND whose post-penalty tier is lower. No binary search, no
        // monotonicity assumption, so it independently checks the finder.
        private static bool BruteForce(PlayerLoadout l, int trueMax, EncounterData d, out int hp)
        {
            var floor = new PlayerHealth(trueMax, 0, 1f).Floor;
            var top = Tier(l, trueMax, d);
            for (hp = floor; hp <= trueMax; hp++)
            {
                if (Tier(l, hp, d) != top)
                {
                    continue;
                }

                if (Tier(l, PenaltyFrom(trueMax, hp), d) > top)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void Finder_MatchesBruteForceSpecReading_ForBorderlineFixture()
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            var found = BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var r);
            var expected = BruteForce(loadout, TrueMax, data, out var expectedHp);

            Assert.AreEqual(expected, found);
            Assert.IsTrue(found, "fixture should have a borderline HP");
            Assert.AreEqual(expectedHp, r.Hp);
        }

        [TestCase(12, 120, 10, 1f)]
        [TestCase(20, 90, 8, 1f)]
        [TestCase(40, 200, 15, 0.5f)]
        [TestCase(500, 20, 1, 0.5f)]
        public void Finder_MatchesBruteForceSpecReading_AcrossFixtures(int power, int enemyHp, int dmg, float speed)
        {
            var loadout = Loadout(power);
            var data = MakeEncounter(enemyHp, dmg, speed);
            var found = BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var r);
            var expected = BruteForce(loadout, TrueMax, data, out var expectedHp);

            Assert.AreEqual(expected, found);
            if (found)
            {
                Assert.AreEqual(expectedHp, r.Hp);
                Assert.AreEqual(Tier(loadout, TrueMax, data), r.TierAtHp);
                Assert.AreEqual(Tier(loadout, r.HpAfterPenalty, data), r.TierAfterPenalty);
                Assert.Greater((int)r.TierAfterPenalty, (int)r.TierAtHp);
            }
        }

        [Test]
        public void Result_PenaltyArithmetic_IsTwentyFivePercentClampedToFloor()
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            Assert.IsTrue(BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var r));

            var floor = new PlayerHealth(TrueMax, 0, 1f).Floor;
            Assert.AreEqual(20, floor);
            Assert.AreEqual(Mathf.Max(floor, r.Hp - 25), r.HpAfterPenalty);
        }

        [Test]
        public void Finder_IsDeterministic_ForSameSeed()
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var a);
            BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var b);
            Assert.AreEqual(a.Hp, b.Hp);
            Assert.AreEqual(a.TierAtHp, b.TierAtHp);
            Assert.AreEqual(a.HpAfterPenalty, b.HpAfterPenalty);
            Assert.AreEqual(a.TierAfterPenalty, b.TierAfterPenalty);
        }

        [Test]
        public void Finder_ReturnsNone_ForNullEncounter()
        {
            Assert.IsFalse(BorderlineOddsFinder.TryFind(Loadout(5), new PlayerAttributes(), TrueMax, null, Seed, out _));
        }

        [Test]
        public void DebugSet_ClampsToFloorAndTrueMax_AndFiresChanged()
        {
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            var fired = 0;
            health.Changed += () => fired++;

            health.DebugSetCurrentMaxHealth(1);
            Assert.AreEqual(health.Floor, health.CurrentMaxHealth);
            Assert.AreEqual(1, fired);

            health.DebugSetCurrentMaxHealth(10_000);
            Assert.AreEqual(TrueMax, health.CurrentMaxHealth);
            Assert.AreEqual(2, fired);

            health.DebugSetCurrentMaxHealth(62);
            Assert.AreEqual(62, health.CurrentMaxHealth);
            Assert.AreEqual(TrueMax, health.TrueMaxHealth);
        }

        [Test]
        public void DebugSet_DoesNotTouchRegenClock()
        {
            var past = DateTime.UtcNow.AddMinutes(-5).Ticks;
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.LoadState(TrueMax, 80, past);

            health.DebugSetCurrentMaxHealth(62);
            Assert.AreEqual(past, health.LastRegenUtcTicks);
        }

        [Test]
        public void DebugSet_ThenDefeatPenalty_DropsOneTier_FromBorderlineHp()
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            Assert.IsTrue(BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var r));

            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.DebugSetCurrentMaxHealth(r.Hp);
            Assert.AreEqual(r.TierAtHp, Tier(loadout, health.CurrentMaxHealth, data));

            health.ApplyDefeatPenalty();
            Assert.AreEqual(r.HpAfterPenalty, health.CurrentMaxHealth);
            Assert.AreEqual(r.TierAfterPenalty, Tier(loadout, health.CurrentMaxHealth, data));
        }

        [Test]
        public void DebugSet_SurvivesSaveLoadRoundTrip_WithoutNewFields()
        {
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.DebugSetCurrentMaxHealth(62);

            var restored = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            restored.LoadState(health.TrueMaxHealth, health.CurrentMaxHealth, health.LastRegenUtcTicks);
            Assert.AreEqual(62, restored.CurrentMaxHealth);
            Assert.AreEqual(TrueMax, restored.TrueMaxHealth);
        }

        [Test]
        public void DebugSet_IsRestoredByFullyHeal()
        {
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.DebugSetCurrentMaxHealth(62);
            health.FullyHeal();
            Assert.AreEqual(TrueMax, health.CurrentMaxHealth);
        }

        [Test]
        public void DebugSet_IsRestoredByRegen_AfterElapsedTime()
        {
            // 95 s elapsed at 10 HP / 30 s = 3 ticks, so 62 -> 92 (below true max).
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.LoadState(TrueMax, 62, DateTime.UtcNow.AddSeconds(-95).Ticks);
            health.Regenerate();
            Assert.AreEqual(92, health.CurrentMaxHealth);
        }

        [Test]
        public void ApplyDefeatPenalty_AtFloor_StaysAtFloor()
        {
            var health = new PlayerHealth(TrueMax, RegenPerTick, RegenSeconds);
            health.DebugSetCurrentMaxHealth(health.Floor);
            health.ApplyDefeatPenalty();
            Assert.AreEqual(health.Floor, health.CurrentMaxHealth);
        }
    }
}
