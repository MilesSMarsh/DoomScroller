using System;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class WinOddsEstimatorTests
    {
        private static EncounterData MakeEncounter(EncounterType type, int hp, int dmg, float speed)
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.type = type;
            data.enemyHealth = hp;
            data.enemyAttackDamage = dmg;
            data.enemyAttackSpeed = speed;
            return data;
        }

        [Test]
        public void Overpowered_IsLikelyNearOne()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 500, attackSpeed: 2f));
            var data = MakeEncounter(EncounterType.Enemy, 20, 1, 0.5f);
            var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 1000, data, 1);
            Assert.AreEqual(WinOddsTier.Likely, r.Tier);
            Assert.GreaterOrEqual(r.WinFraction, 0.95f);
        }

        [Test]
        public void Hopeless_IsLongShotNearZero()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 0.5f));
            var data = MakeEncounter(EncounterType.Enemy, 100000, 500, 3f);
            var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 50, data, 1);
            Assert.AreEqual(WinOddsTier.LongShot, r.Tier);
            Assert.LessOrEqual(r.WinFraction, 0.05f);
        }

        [Test]
        public void SameSeed_GivesIdenticalResult()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 12, attackSpeed: 1f));
            var data = MakeEncounter(EncounterType.Enemy, 120, 10, 1f);
            var a = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 42);
            var b = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 42);
            Assert.AreEqual(a.WinFraction, b.WinFraction);
            Assert.AreEqual(a.Tier, b.Tier);
        }

        [Test]
        public void LowerStartHp_NeverRaisesWinFraction()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 12, attackSpeed: 1f));
            var data = MakeEncounter(EncounterType.Enemy, 120, 10, 1f);
            var high = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 42);
            var low = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 25, data, 42);
            Assert.LessOrEqual(low.WinFraction, high.WinFraction);
        }

        [Test]
        public void TierBoundaries_MapExactlyAtThresholds()
        {
            Assert.AreEqual(WinOddsTier.Likely, WinOddsEstimator.TierFor(WinOddsEstimator.LikelyThreshold));
            Assert.AreEqual(WinOddsTier.Risky, WinOddsEstimator.TierFor(WinOddsEstimator.LikelyThreshold - 0.001f));
            Assert.AreEqual(WinOddsTier.Risky, WinOddsEstimator.TierFor(WinOddsEstimator.RiskyThreshold));
            Assert.AreEqual(WinOddsTier.LongShot, WinOddsEstimator.TierFor(WinOddsEstimator.RiskyThreshold - 0.001f));
            Assert.AreEqual(WinOddsTier.LongShot, WinOddsEstimator.TierFor(0f));
            Assert.AreEqual(WinOddsTier.Likely, WinOddsEstimator.TierFor(1f));
        }

        [Test]
        public void NonFightTypes_ReturnOneAndLikely()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f));
            foreach (var type in new[] { EncounterType.Treasure, EncounterType.Tavern, EncounterType.Resource,
                         EncounterType.Landing, EncounterType.WanderingMerchant, EncounterType.Dungeon })
            {
                var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, MakeEncounter(type, 1, 1, 1f), 1);
                Assert.AreEqual(1f, r.WinFraction);
                Assert.AreEqual(WinOddsTier.Likely, r.Tier);
            }
        }

        [Test]
        public void NonPositiveRunCount_Throws()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f));
            var data = MakeEncounter(EncounterType.Enemy, 10, 1, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 1, -5));
        }

        // Shared mid-table matchup (roughly even) used by the determinism and derivation tests.
        private static ItemData MidMatchupWeapon() => TestFixtures.MakeWeapon(power: 12, attackSpeed: 1f);
        private static EncounterData MidMatchupEncounter() => MakeEncounter(EncounterType.Enemy, 120, 10, 1f);

        [Test]
        public void Estimate_MatchesManualRunsThroughCombatResolver()
        {
            // Proves each run is CombatResolver.CreateSimulator + RunUntilDeathOrVictory with
            // its own Random derived from the injected source (no duplicated fight math).
            const int runs = 64;
            var loadout = TestFixtures.MakeLoadout(MidMatchupWeapon());
            var data = MidMatchupEncounter();

            var derive = new System.Random(7);
            var expectedWins = 0;
            for (var i = 0; i < runs; i++)
            {
                var sim = CombatResolver.CreateSimulator(loadout, new PlayerAttributes(), new GameSettings(), data,
                    100, new System.Random(derive.Next()));
                sim.RunUntilDeathOrVictory();
                if (sim.IsEnemyDead)
                {
                    expectedWins++;
                }
            }

            var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 7, runs);
            Assert.AreEqual((float)expectedWins / runs, r.WinFraction, 0f);
        }

        [Test]
        public void IntSeed_MatchesSystemRandomSourceWithSameSeed()
        {
            var loadout = TestFixtures.MakeLoadout(MidMatchupWeapon());
            var data = MidMatchupEncounter();
            var fromInt = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 5);
            var fromRandom = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data,
                new System.Random(5));
            Assert.AreEqual(fromInt.WinFraction, fromRandom.WinFraction, 0f);
            Assert.AreEqual(fromInt.Tier, fromRandom.Tier);
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentFractions()
        {
            // Guards against the seed being ignored entirely. Fixed seeds, so this is deterministic.
            var loadout = TestFixtures.MakeLoadout(MidMatchupWeapon());
            var data = MidMatchupEncounter();
            var distinct = new System.Collections.Generic.HashSet<float>();
            for (var seed = 1; seed <= 10; seed++)
            {
                distinct.Add(WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, seed, 64).WinFraction);
            }

            Assert.GreaterOrEqual(distinct.Count, 2);
        }

        [Test]
        public void WinFraction_IsWinsOverRunCount_AndTierMatchesIt()
        {
            const int runs = 10;
            var loadout = TestFixtures.MakeLoadout(MidMatchupWeapon());
            var data = MidMatchupEncounter();
            var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 3, runs);

            Assert.GreaterOrEqual(r.WinFraction, 0f);
            Assert.LessOrEqual(r.WinFraction, 1f);
            var wins = r.WinFraction * runs;
            Assert.AreEqual(Math.Round(wins), wins, 1e-4, "fraction must be an exact wins/runs ratio");
            Assert.AreEqual(WinOddsEstimator.TierFor(r.WinFraction), r.Tier);
        }

        [Test]
        public void DefaultRunCount_IsInGddRangeAndMatchesExplicitCall()
        {
            Assert.GreaterOrEqual(WinOddsEstimator.DefaultRunCount, 50);
            Assert.LessOrEqual(WinOddsEstimator.DefaultRunCount, 100);

            var loadout = TestFixtures.MakeLoadout(MidMatchupWeapon());
            var data = MidMatchupEncounter();
            var implicitDefault = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 9);
            var explicitDefault = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 9,
                WinOddsEstimator.DefaultRunCount);
            Assert.AreEqual(explicitDefault.WinFraction, implicitDefault.WinFraction, 0f);
        }

        [Test]
        public void OneRun_IsAllOrNothing()
        {
            var data = MakeEncounter(EncounterType.Enemy, 20, 1, 0.5f);
            var strong = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 500, attackSpeed: 2f));
            var weak = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 0.5f));
            var hardEnemy = MakeEncounter(EncounterType.Enemy, 100000, 500, 3f);

            var win = WinOddsEstimator.Estimate(strong, new PlayerAttributes(), 1000, data, 1, 1);
            Assert.AreEqual(1f, win.WinFraction);
            Assert.AreEqual(WinOddsTier.Likely, win.Tier);

            var loss = WinOddsEstimator.Estimate(weak, new PlayerAttributes(), 50, hardEnemy, 1, 1);
            Assert.AreEqual(0f, loss.WinFraction);
            Assert.AreEqual(WinOddsTier.LongShot, loss.Tier);
        }

        [Test]
        public void TierFor_JustAboveAndBelowThresholds()
        {
            Assert.AreEqual(WinOddsTier.Likely, WinOddsEstimator.TierFor(WinOddsEstimator.LikelyThreshold + 0.0001f));
            Assert.AreEqual(WinOddsTier.Risky, WinOddsEstimator.TierFor(WinOddsEstimator.RiskyThreshold + 0.0001f));
            Assert.AreEqual(WinOddsTier.LongShot, WinOddsEstimator.TierFor(WinOddsEstimator.RiskyThreshold - 0.0001f));
            Assert.AreEqual(WinOddsTier.Risky, WinOddsEstimator.TierFor(0.5f));
        }

        [Test]
        public void NonFightTypes_DoNotTouchLoadoutOrAttributes()
        {
            // Null loadout/attributes would throw inside CreateSimulator, so a clean return
            // proves the short-circuit happens before any simulator is built.
            foreach (var type in new[] { EncounterType.Treasure, EncounterType.Tavern, EncounterType.Resource,
                         EncounterType.Landing, EncounterType.WanderingMerchant, EncounterType.Dungeon })
            {
                var r = WinOddsEstimator.Estimate(null, null, 100, MakeEncounter(type, 1, 1, 1f), 1);
                Assert.AreEqual(1f, r.WinFraction, 0f, type.ToString());
                Assert.AreEqual(WinOddsTier.Likely, r.Tier, type.ToString());
            }
        }

        [Test]
        public void NonFightTypes_IsNonFightMatchesSpecList()
        {
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.Treasure));
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.Tavern));
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.Resource));
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.Landing));
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.WanderingMerchant));
            Assert.IsFalse(WinOddsEstimator.IsNonFight(EncounterType.Enemy));
            Assert.IsFalse(WinOddsEstimator.IsNonFight(EncounterType.Ambush));
            Assert.IsTrue(WinOddsEstimator.IsNonFight(EncounterType.Dungeon));
        }

        [Test]
        public void Dungeon_ShortCircuitsToLikelyWithoutSimulating()
        {
            // Null loadout/attributes would throw if a fight were run.
            var r = WinOddsEstimator.Estimate(null, null, 100, MakeEncounter(EncounterType.Dungeon, 100000, 500, 3f), 1);
            Assert.AreEqual(1f, r.WinFraction, 0f);
            Assert.AreEqual(WinOddsTier.Likely, r.Tier);
        }

        [Test]
        public void NullArguments_ThrowArgumentNullException()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f));
            var fight = MakeEncounter(EncounterType.Enemy, 10, 1, 1f);
            Assert.Throws<ArgumentNullException>(() =>
                WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, null, 1));
            Assert.Throws<ArgumentNullException>(() =>
                WinOddsEstimator.Estimate(null, new PlayerAttributes(), 100, fight, 1));
            Assert.Throws<ArgumentNullException>(() =>
                WinOddsEstimator.Estimate(loadout, null, 100, fight, 1));
        }

        [Test]
        public void Ambush_IsSimulatedNotShortCircuited()
        {
            // Ambush fights like Enemy; a hopeless matchup must not come back as a free Likely win.
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 0.5f));
            var data = MakeEncounter(EncounterType.Ambush, 100000, 500, 3f);
            var r = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 50, data, 1);
            Assert.AreEqual(WinOddsTier.LongShot, r.Tier);
            Assert.LessOrEqual(r.WinFraction, 0.05f);
        }

        [Test]
        public void NonFightTypes_StillRejectNonPositiveRunCount()
        {
            // Run-count validation runs before the non-fight short-circuit, so a bad count
            // is always an error, regardless of encounter type.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WinOddsEstimator.Estimate(null, null, 100, MakeEncounter(EncounterType.Treasure, 1, 1, 1f), 1, 0));
        }
    }
}
