using System.Reflection;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Task 023 gap coverage: the null-feed paths of the shared debug actions, the Editor
    // menu validators (greyed out outside Play Mode), and the finder's "never borderline"
    // and tiny-ceiling edges that the existing finder tests don't pin down.
    public class BorderlineOddsDebugGapTests
    {
        private const int Seed = 7;
        private const int TrueMax = 100;

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

        [Test]
        public void SetBorderlineHp_NullFeed_ReportsAndReturnsNotOk()
        {
            var message = BorderlineOddsDebugActions.SetBorderlineHp(null, out var ok);
            Assert.IsFalse(ok);
            StringAssert.Contains("No FeedController", message);
        }

        [Test]
        public void ApplyPenalty_NullFeed_ReportsWithoutThrowing()
        {
            string message = null;
            Assert.DoesNotThrow(() => message = BorderlineOddsDebugActions.ApplyPenalty(null));
            StringAssert.Contains("No FeedController", message);
        }

        [Test]
        public void MenuValidators_AreGreyedOut_OutsidePlayMode()
        {
            Assert.IsFalse(Application.isPlaying, "EditMode tests run outside Play Mode");
            var menuType = typeof(DoomScroller.Editor.BorderlineOddsDebugMenu);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

            var borderline = menuType.GetMethod("ValidateBorderline", flags);
            var penalty = menuType.GetMethod("ValidatePenalty", flags);
            Assert.NotNull(borderline, "ValidateBorderline missing");
            Assert.NotNull(penalty, "ValidatePenalty missing");
            Assert.IsFalse((bool)borderline.Invoke(null, null));
            Assert.IsFalse((bool)penalty.Invoke(null, null));
        }

        [Test]
        public void Finder_LikelyAtEveryHp_ReturnsNone_AndEveryHpIsLikely()
        {
            var loadout = Loadout(500);
            var data = MakeEncounter(20, 1, 0.5f);
            var floor = new PlayerHealth(TrueMax, 0, 1f).Floor;

            Assert.AreEqual(WinOddsTier.Likely, Tier(loadout, floor, data));
            Assert.AreEqual(WinOddsTier.Likely, Tier(loadout, TrueMax / 2, data));
            Assert.AreEqual(WinOddsTier.Likely, Tier(loadout, TrueMax, data));
            Assert.IsFalse(BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out _));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void Finder_TinyCeiling_StaysWithinFloorAndMax(int trueMax)
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            var floor = new PlayerHealth(trueMax, 0, 1f).Floor;
            Assert.GreaterOrEqual(floor, 1);

            bool found = false;
            BorderlineOddsFinder.Result r = default;
            Assert.DoesNotThrow(() => found = BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(),
                trueMax, data, Seed, out r));
            if (found)
            {
                Assert.GreaterOrEqual(r.Hp, floor);
                Assert.LessOrEqual(r.Hp, trueMax);
                Assert.GreaterOrEqual(r.HpAfterPenalty, floor);
                Assert.LessOrEqual(r.HpAfterPenalty, r.Hp);
            }
        }
    }
}
