using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class BorderlineOddsFinderTests
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
        public void FindsHpWhosePenaltyDropsATier()
        {
            var loadout = Loadout(12);
            var data = MakeEncounter(120, 10, 1f);
            var found = BorderlineOddsFinder.TryFind(loadout, new PlayerAttributes(), TrueMax, data, Seed, out var r);
            Assert.IsTrue(found);
            Assert.AreEqual(Tier(loadout, TrueMax, data), Tier(loadout, r.Hp, data));
            Assert.Greater((int)Tier(loadout, r.HpAfterPenalty, data), (int)Tier(loadout, r.Hp, data));
            Assert.GreaterOrEqual(r.Hp, new PlayerHealth(TrueMax, 0, 1f).Floor);
            Assert.LessOrEqual(r.Hp, TrueMax);
            if (r.Hp > new PlayerHealth(TrueMax, 0, 1f).Floor)
            {
                Assert.AreNotEqual(r.TierAtHp, Tier(loadout, r.Hp - 1, data));
            }
        }

        [Test]
        public void ReturnsNone_WhenLikelyAcrossWholeRange()
        {
            var data = MakeEncounter(20, 1, 0.5f);
            Assert.IsFalse(BorderlineOddsFinder.TryFind(Loadout(500), new PlayerAttributes(), TrueMax, data, Seed, out _));
        }

        [Test]
        public void ReturnsNone_WhenLongShotAtTrueMax()
        {
            var data = MakeEncounter(100000, 500, 3f);
            Assert.IsFalse(BorderlineOddsFinder.TryFind(Loadout(1), new PlayerAttributes(), TrueMax, data, Seed, out _));
        }

        [Test]
        public void ReturnsNone_ForNonFight()
        {
            var data = MakeEncounter(1, 1, 1f);
            data.type = EncounterType.Treasure;
            Assert.IsFalse(BorderlineOddsFinder.TryFind(Loadout(5), new PlayerAttributes(), TrueMax, data, Seed, out _));
        }
    }
}
