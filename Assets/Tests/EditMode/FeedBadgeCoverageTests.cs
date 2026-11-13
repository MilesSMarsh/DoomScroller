using System;
using System.Collections.Generic;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA coverage for the feed win-odds badge (task 002): exhaustive enum sweeps, label
    // hygiene, and cross-checks between FeedBadge and WinOddsEstimator. Plain C# only.
    public class FeedBadgeCoverageTests
    {
        // Expected visibility for every EncounterType. Keyed explicitly so that appending a
        // new enum member fails EveryEncounterType_IsClassified until someone decides.
        private static readonly Dictionary<EncounterType, bool> ExpectedVisible = new Dictionary<EncounterType, bool>
        {
            { EncounterType.Treasure, false },
            { EncounterType.Enemy, true },
            { EncounterType.Dungeon, false },
            { EncounterType.Tavern, false },
            { EncounterType.Resource, false },
            { EncounterType.Landing, false },
            { EncounterType.WanderingMerchant, false },
            { EncounterType.Ambush, true },
        };

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
        public void EveryEncounterType_IsClassified()
        {
            var all = (EncounterType[])Enum.GetValues(typeof(EncounterType));
            Assert.AreEqual(all.Length, ExpectedVisible.Count, "New EncounterType added; decide its badge visibility.");
            foreach (var type in all)
            {
                Assert.IsTrue(ExpectedVisible.ContainsKey(type), type.ToString());
            }
        }

        [Test]
        public void IsVisible_MatchesExpectedForEveryEncounterType()
        {
            foreach (var pair in ExpectedVisible)
            {
                Assert.AreEqual(pair.Value, FeedBadge.IsVisible(pair.Key), pair.Key.ToString());
            }
        }

        [Test]
        public void IsVisible_AgreesWithEstimatorIsNonFight_ForEveryType()
        {
            // Drift guard: the badge must be hidden exactly where the estimator skips the sim.
            foreach (var type in (EncounterType[])Enum.GetValues(typeof(EncounterType)))
            {
                Assert.AreEqual(!WinOddsEstimator.IsNonFight(type), FeedBadge.IsVisible(type), type.ToString());
            }
        }

        [Test]
        public void HiddenBadge_TypesShortCircuitWithoutSimulating()
        {
            // Hidden badges come from non-fight types; those must return Likely without touching
            // loadout/attributes (null would throw if a fight ran). Visible types must simulate.
            foreach (var pair in ExpectedVisible)
            {
                var data = MakeEncounter(pair.Key, 100000, 500, 3f);
                if (pair.Value)
                {
                    Assert.Throws<ArgumentNullException>(() =>
                        WinOddsEstimator.Estimate(null, null, 100, data, 1), pair.Key.ToString());
                }
                else
                {
                    var r = WinOddsEstimator.Estimate(null, null, 100, data, 1);
                    Assert.AreEqual(WinOddsTier.Likely, r.Tier, pair.Key.ToString());
                    Assert.AreEqual(1f, r.WinFraction, 0f, pair.Key.ToString());
                }
            }
        }

        [Test]
        public void LabelFor_EveryTierHasNonEmptyDistinctLabel()
        {
            var seen = new HashSet<string>();
            foreach (var tier in (WinOddsTier[])Enum.GetValues(typeof(WinOddsTier)))
            {
                var label = FeedBadge.LabelFor(tier);
                Assert.IsFalse(string.IsNullOrWhiteSpace(label), tier.ToString());
                Assert.IsTrue(seen.Add(label), $"Duplicate label '{label}' for {tier}");
            }

            Assert.AreEqual(Enum.GetValues(typeof(WinOddsTier)).Length, seen.Count);
        }

        [Test]
        public void LabelFor_NeverContainsPercentOrDigit()
        {
            foreach (var tier in (WinOddsTier[])Enum.GetValues(typeof(WinOddsTier)))
            {
                var label = FeedBadge.LabelFor(tier);
                Assert.IsFalse(label.Contains("%"), $"{tier}: '{label}' contains %");
                foreach (var c in label)
                {
                    Assert.IsFalse(char.IsDigit(c), $"{tier}: '{label}' contains digit '{c}'");
                }
            }
        }

        [Test]
        public void LabelFor_ExactWordsForEachTier()
        {
            // Pins the player-facing copy so a silent rename shows up in review.
            Assert.AreEqual("Likely", FeedBadge.LabelFor(WinOddsTier.Likely));
            Assert.AreEqual("Risky", FeedBadge.LabelFor(WinOddsTier.Risky));
            Assert.AreEqual("Long shot", FeedBadge.LabelFor(WinOddsTier.LongShot));
        }

        [Test]
        public void ColorFor_EveryTierHasOpaqueEnoughColourInRange()
        {
            foreach (var tier in (WinOddsTier[])Enum.GetValues(typeof(WinOddsTier)))
            {
                var c = FeedBadge.ColorFor(tier);
                foreach (var channel in new[] { c.r, c.g, c.b, c.a })
                {
                    Assert.GreaterOrEqual(channel, 0f, tier.ToString());
                    Assert.LessOrEqual(channel, 1f, tier.ToString());
                }

                Assert.Greater(c.a, 0f, $"{tier} badge would be invisible");
            }
        }

        [Test]
        public void ColorFor_IsDeterministicPerTier()
        {
            foreach (var tier in (WinOddsTier[])Enum.GetValues(typeof(WinOddsTier)))
            {
                Assert.AreEqual(FeedBadge.ColorFor(tier), FeedBadge.ColorFor(tier), tier.ToString());
            }
        }

        [Test]
        public void ColorFor_PlaceholderSemantics_GreenLikelyRedLongShot()
        {
            // Traffic-light placeholder: Likely reads green, Long shot reads red.
            var likely = FeedBadge.ColorFor(WinOddsTier.Likely);
            var longShot = FeedBadge.ColorFor(WinOddsTier.LongShot);
            Assert.Greater(likely.g, likely.r, "Likely should be green-dominant");
            Assert.Greater(longShot.r, longShot.g, "Long shot should be red-dominant");
        }

        [Test]
        public void Pipeline_StrongLoadout_ShowsLikely()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 500, attackSpeed: 2f));
            var data = MakeEncounter(EncounterType.Enemy, 20, 1, 0.5f);
            var odds = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 1000, data, 1);
            Assert.AreEqual("Likely", FeedBadge.LabelFor(odds.Tier));
        }

        [Test]
        public void Pipeline_HopelessLoadout_ShowsLongShot()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 1, attackSpeed: 0.5f));
            var data = MakeEncounter(EncounterType.Enemy, 100000, 500, 3f);
            var odds = WinOddsEstimator.Estimate(loadout, new PlayerAttributes(), 50, data, 1);
            Assert.AreEqual("Long shot", FeedBadge.LabelFor(odds.Tier));
        }

        [Test]
        public void Pipeline_NonFightCard_ReportsHiddenAndLikely()
        {
            // A hidden badge still carries a valid tier from the estimator, so SetLocked's
            // re-apply path (new WinOddsResult(0f, _tier)) stays on a real tier.
            var odds = WinOddsEstimator.Estimate(null, null, 100, MakeEncounter(EncounterType.Tavern, 1, 1, 1f), 1);
            Assert.IsFalse(FeedBadge.IsVisible(EncounterType.Tavern));
            Assert.AreEqual("Likely", FeedBadge.LabelFor(odds.Tier));
        }
    }
}
