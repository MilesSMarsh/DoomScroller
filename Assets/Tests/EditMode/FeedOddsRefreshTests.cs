using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Covers task 022: feed odds re-estimate when the fight start HP changes.
    // MaxHealthChangeTracker is exercised against real PlayerHealth events (no scene, no Start).
    public class FeedOddsRefreshTests
    {
        private const BindingFlags AnyInstance =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        private static EncounterData MakeEncounter(EncounterType type, int hp, int dmg, float speed)
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.type = type;
            data.enemyHealth = hp;
            data.enemyAttackDamage = dmg;
            data.enemyAttackSpeed = speed;
            return data;
        }

        // ---- Tracker driven by real PlayerHealth.Changed --------------------------------

        [Test]
        public void Defeat_ChangesCurrentMax_TrackerRefreshes()
        {
            var health = new PlayerHealth(100, 10, 30f);
            var tracker = new MaxHealthChangeTracker();
            tracker.ShouldRefresh(health.CurrentMaxHealth);
            var refreshes = new List<bool>();
            health.Changed += () => refreshes.Add(tracker.ShouldRefresh(health.CurrentMaxHealth));

            health.ApplyDefeatPenalty(); // 100 -> 75

            CollectionAssert.AreEqual(new[] { true }, refreshes);
        }

        [Test]
        public void DefeatAtFloor_FiresChanged_ButTrackerDoesNotRefresh()
        {
            var health = new PlayerHealth(100, 10, 30f);
            var tracker = new MaxHealthChangeTracker();
            tracker.ShouldRefresh(health.CurrentMaxHealth);
            var refreshes = new List<bool>();
            health.Changed += () => refreshes.Add(tracker.ShouldRefresh(health.CurrentMaxHealth));

            for (var i = 0; i < 6; i++)
            {
                health.ApplyDefeatPenalty();
            }

            // 100 -> 75 -> 50 -> 25 -> 20 (floor), then two more defeats at the floor.
            Assert.AreEqual(health.Floor, health.CurrentMaxHealth);
            Assert.AreEqual(6, refreshes.Count, "Changed should fire once per defeat");
            CollectionAssert.AreEqual(new[] { true, true, true, true, false, false }, refreshes);
        }

        [Test]
        public void RegenTick_RaisesCurrentMax_TrackerRefreshes()
        {
            var health = new PlayerHealth(100, 10, 30f);
            health.ApplyDefeatPenalty(); // 75
            var tracker = new MaxHealthChangeTracker();
            tracker.ShouldRefresh(health.CurrentMaxHealth);
            var refreshes = new List<bool>();
            health.Changed += () => refreshes.Add(tracker.ShouldRefresh(health.CurrentMaxHealth));

            health.FullyHeal(); // 75 -> 100

            CollectionAssert.AreEqual(new[] { true }, refreshes);
        }

        [Test]
        public void TrueMaxIncrease_ThatMovesCurrentMax_TrackerRefreshes()
        {
            var health = new PlayerHealth(100, 10, 30f);
            health.ApplyDefeatPenalty(); // current 75
            var tracker = new MaxHealthChangeTracker();
            tracker.ShouldRefresh(health.CurrentMaxHealth);
            var refreshes = new List<bool>();
            health.Changed += () => refreshes.Add(tracker.ShouldRefresh(health.CurrentMaxHealth));

            health.SetTrueMaxHealth(110); // current 75 -> 85

            Assert.AreEqual(85, health.CurrentMaxHealth);
            CollectionAssert.AreEqual(new[] { true }, refreshes);
        }

        [Test]
        public void NoOpTrueMaxChange_DoesNotFireChanged()
        {
            var health = new PlayerHealth(100, 10, 30f);
            var fired = 0;
            health.Changed += () => fired++;

            health.SetTrueMaxHealth(100);
            health.FullyHeal();
            health.RestoreMaxHealth(10);

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Sequence_RefreshesOnlyWhenCurrentMaxMoves()
        {
            var tracker = new MaxHealthChangeTracker();
            var values = new[] { 100, 100, 75, 75, 75, 100, 100 };
            var expected = new[] { true, false, true, false, false, true, false };
            var actual = values.Select(v => tracker.ShouldRefresh(v)).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        // ---- FeedController teardown ---------------------------------------------------

        // Not covered here: OnDestroy's unsubscribe from Health.Changed / Loadout.Changed only runs
        // when profileController is a live object. A GetUninitializedObject fake reads as null to
        // Unity's == operator, so the guard returns early and the teardown can't be observed in EditMode.
        // Verified by code review; needs a manual check (or a Play Mode test).

        [Test]
        public void OnDestroy_WithoutProfile_DoesNotThrow()
        {
            var feed = (FeedController)RuntimeHelpers.GetUninitializedObject(typeof(FeedController));
            Assert.DoesNotThrow(() =>
                typeof(FeedController).GetMethod("OnDestroy", AnyInstance).Invoke(feed, null));
        }

        // ---- Odds input: lower start HP must not raise the win fraction -----------------

        [Test]
        public void LowerStartHp_NeverRaisesWinFraction_AcrossSeeds()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 12, attackSpeed: 1f));
            var data = MakeEncounter(EncounterType.Enemy, 120, 10, 1f);
            for (var seed = 1; seed <= 20; seed++)
            {
                var high = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, seed);
                var mid = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 60, data, seed);
                var low = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 20, data, seed);
                Assert.LessOrEqual(mid.WinFraction, high.WinFraction, $"seed {seed}: 60 HP beat 100 HP");
                Assert.LessOrEqual(low.WinFraction, mid.WinFraction, $"seed {seed}: 20 HP beat 60 HP");
            }
        }

        [Test]
        public void FloorHp_IsStrictlyWorseThanFullHp_ForBorderlineFight()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 12, attackSpeed: 1f));
            var data = MakeEncounter(EncounterType.Enemy, 120, 10, 1f);
            var full = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 100, data, 7);
            var floor = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 20, data, 7);
            Assert.Less(floor.WinFraction, full.WinFraction);
        }
    }
}
