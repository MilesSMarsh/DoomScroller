using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class RewardGeneratorTests
    {
        [Test]
        public void RollRarity_NullOrEmptyOrAllZeroTable_AlwaysReturnsCommon()
        {
            var rng = new System.Random(1);
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(null, rng));
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(new float[0], rng));
            Assert.AreEqual(ItemRarity.Common, RewardGenerator.RollRarity(new float[5], rng));
        }

        [Test]
        public void RollRarity_SingleNonZeroRarity_AlwaysWinsRegardlessOfRng()
        {
            var table = new float[5];
            table[(int)ItemRarity.Legendary] = 1f;

            for (var seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(ItemRarity.Legendary, RewardGenerator.RollRarity(table, new System.Random(seed)));
            }
        }

        [Test]
        public void RollRarity_WeightedDistribution_MatchesTableProportions()
        {
            var table = new float[5];
            table[(int)ItemRarity.Common] = 70f;
            table[(int)ItemRarity.Uncommon] = 25f;
            table[(int)ItemRarity.Rare] = 5f;

            var rng = new System.Random(12345);
            const int trials = 20000;
            var counts = new int[5];
            for (var i = 0; i < trials; i++)
            {
                counts[(int)RewardGenerator.RollRarity(table, rng)]++;
            }

            // Generous tolerance — statistical, not exact.
            Assert.AreEqual(0.70f, counts[(int)ItemRarity.Common] / (float)trials, 0.02f);
            Assert.AreEqual(0.25f, counts[(int)ItemRarity.Uncommon] / (float)trials, 0.02f);
            Assert.AreEqual(0.05f, counts[(int)ItemRarity.Rare] / (float)trials, 0.02f);
            Assert.AreEqual(0, counts[(int)ItemRarity.Legendary]);
            Assert.AreEqual(0, counts[(int)ItemRarity.Mythic]);
        }

        [Test]
        public void RollRarity_TableNotSummingToOneHundred_StillNormalizesCorrectly()
        {
            // 2:1 ratio regardless of the total being 30, not 100.
            var table = new float[5];
            table[(int)ItemRarity.Common] = 20f;
            table[(int)ItemRarity.Uncommon] = 10f;

            var rng = new System.Random(999);
            const int trials = 10000;
            var commonCount = 0;
            for (var i = 0; i < trials; i++)
            {
                if (RewardGenerator.RollRarity(table, rng) == ItemRarity.Common)
                {
                    commonCount++;
                }
            }

            Assert.AreEqual(2f / 3f, commonCount / (float)trials, 0.03f);
        }

        [Test]
        public void DescribeLootTable_NullOrAllZeroTable_FallsBackToTheOriginalGenericDefault()
        {
            Assert.AreEqual("+50 XP · Common Gear", RewardGenerator.DescribeLootTable(null, 50));
            Assert.AreEqual("+50 XP · Common Gear", RewardGenerator.DescribeLootTable(new float[5], 50));
        }

        [Test]
        public void DescribeLootTable_NamesOnlyRarities_NeverASpecificItem()
        {
            var table = new float[5];
            table[(int)ItemRarity.Common] = 70f;
            table[(int)ItemRarity.Uncommon] = 25f;
            table[(int)ItemRarity.Rare] = 5f;

            var text = RewardGenerator.DescribeLootTable(table, 80);

            Assert.AreEqual("+80 XP · 70% Common, 25% Uncommon, 5% Rare Gear", text);
        }

        [Test]
        public void DescribeLootTable_SkipsZeroWeightRarities()
        {
            var table = new float[5];
            table[(int)ItemRarity.Common] = 1f;
            table[(int)ItemRarity.Mythic] = 1f;

            var text = RewardGenerator.DescribeLootTable(table, 10);

            Assert.AreEqual("+10 XP · 50% Common, 50% Mythic Gear", text);
        }

        [Test]
        public void DescribeLootTable_NormalizesAgainstItsOwnTotal_NotRequiringExactlyOneHundred()
        {
            var table = new float[5];
            table[(int)ItemRarity.Common] = 20f;
            table[(int)ItemRarity.Uncommon] = 10f;

            var text = RewardGenerator.DescribeLootTable(table, 10);

            Assert.AreEqual("+10 XP · 67% Common, 33% Uncommon Gear", text);
        }
    }
}
