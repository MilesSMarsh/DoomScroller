using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoomScroller.Tests.EditMode
{
    // Exercises FeedController.DrawNext's shuffle-bag floor handling without a scene.
    // The controller and profile are built with RuntimeHelpers.GetUninitializedObject so
    // no Awake/Start (save-file reads, bootstrappers) runs; only the fields DrawNext touches
    // are populated, via reflection. _rng is seeded so every run is deterministic.
    public class FeedDrawNextTests
    {
        private const BindingFlags AnyInstance =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        // TrueMax 100 -> Floor 20.
        private const int FloorHp = 20;
        private const int FullHp = 100;

        private FeedController _feed;
        private PlayerHealth _health;

        [SetUp]
        public void SetUp()
        {
            _feed = (FeedController)RuntimeHelpers.GetUninitializedObject(typeof(FeedController));
            var profile = (ProfileController)RuntimeHelpers.GetUninitializedObject(typeof(ProfileController));
            _health = new PlayerHealth(100, 10, 30f);
            SetField(profile, "_health", _health);
            SetField(_feed, "profileController", profile);
            SetField(_feed, "_shuffleBag", new List<int>());
            SetField(_feed, "_rng", new System.Random(1234));
        }

        [Test]
        public void DrawNext_AtFloor_NeverDrawsAmbush()
        {
            SetPool(EncounterType.Ambush, EncounterType.Enemy, EncounterType.Treasure,
                EncounterType.Resource, EncounterType.Dungeon, EncounterType.Tavern,
                EncounterType.WanderingMerchant);
            SetAtFloor(true);

            for (var i = 0; i < 500; i++)
            {
                Assert.AreNotEqual(EncounterType.Ambush, Draw().type, $"Ambush drawn on draw {i} at floor");
            }
        }

        [Test]
        public void DrawNext_AboveFloor_CanDrawAmbush()
        {
            SetPool(EncounterType.Ambush, EncounterType.Enemy);
            SetAtFloor(false);

            var drawn = new List<EncounterType> { Draw().type, Draw().type };

            CollectionAssert.Contains(drawn, EncounterType.Ambush);
        }

        [Test]
        public void DrawNext_AtFloor_EachDrawableEntryOncePerPass()
        {
            var pool = new[]
            {
                EncounterType.Ambush, EncounterType.Enemy, EncounterType.Treasure,
                EncounterType.Tavern, EncounterType.Resource,
            };
            SetPool(pool);
            SetAtFloor(true);

            var titles = new HashSet<string>();
            for (var i = 0; i < 4; i++)
            {
                var card = Draw();
                Assert.AreNotEqual(EncounterType.Ambush, card.type);
                Assert.IsTrue(titles.Add(card.title), $"'{card.title}' drawn twice in one pass");
            }
        }

        [Test]
        public void DrawNext_OffFloor_EachEntryOncePerPass()
        {
            SetPool(EncounterType.Ambush, EncounterType.Enemy, EncounterType.Treasure,
                EncounterType.Tavern, EncounterType.Resource);
            SetAtFloor(false);

            var titles = new HashSet<string>();
            for (var i = 0; i < 5; i++)
            {
                Assert.IsTrue(titles.Add(Draw().title), "entry drawn twice within one pass");
            }

            Assert.AreEqual(5, titles.Count);
        }

        [Test]
        public void DrawNext_SkippedAmbush_StaysInBag_DrawnLaterInSamePass()
        {
            SetPool(EncounterType.Ambush, EncounterType.Enemy, EncounterType.Treasure);
            SetAtFloor(true);

            // Two drawable entries come out at the floor; the Ambush must remain in the bag.
            var first = Draw();
            var second = Draw();
            Assert.AreNotEqual(EncounterType.Ambush, first.type);
            Assert.AreNotEqual(EncounterType.Ambush, second.type);
            Assert.AreNotEqual(first.title, second.title);

            // Player regens off the floor: the only thing left in this pass is the Ambush.
            SetAtFloor(false);
            Assert.AreEqual(EncounterType.Ambush, Draw().type);
        }

        [Test]
        public void DrawNext_AllUndrawableInBag_RefillsAndDrawsDrawable()
        {
            SetPool(EncounterType.Ambush, EncounterType.Enemy);
            SetAtFloor(true);

            // First draw takes the Enemy, leaving only the Ambush in the bag.
            Assert.AreEqual(EncounterType.Enemy, Draw().type);

            // Bag now holds only undrawable entries: it must refill and draw the Enemy again
            // (a new pass), not hang and not return the Ambush.
            var again = Draw();
            Assert.AreEqual(EncounterType.Enemy, again.type);
            // The refilled pass has two entries; one was just drawn, so one remains.
            Assert.AreEqual(1, GetBagCount());
        }

        [Test]
        public void DrawNext_NoDrawableEntryInPool_WarnsAndReturnsAnEntry()
        {
            SetPool(EncounterType.Ambush, EncounterType.Ambush);
            SetAtFloor(true);

            LogAssert.Expect(LogType.Warning, new Regex("no drawable encounter"));
            var card = Draw();

            Assert.IsNotNull(card);
            Assert.AreEqual(EncounterType.Ambush, card.type);
        }

        [Test]
        public void IsAtFloor_TinyTrueMax_FloorIsOne()
        {
            // TrueMax 3 -> 3 * 0.2 rounds to 1; Floor is clamped to at least 1.
            var health = new PlayerHealth(3, 1, 30f);
            Assert.AreEqual(1, health.Floor);
            Assert.IsFalse(health.IsAtFloor);

            health.LoadState(3, 1, health.LastRegenUtcTicks);
            Assert.IsTrue(health.IsAtFloor);
        }

        [Test]
        public void IsAtFloor_CurrentBelowFloor_IsTrue()
        {
            // Defensive: a corrupt or hand-edited save below the floor still counts as at-floor.
            var health = new PlayerHealth(100, 10, 30f);
            health.LoadState(100, 5, health.LastRegenUtcTicks);
            Assert.IsTrue(health.IsAtFloor);
        }

        private EncounterData Draw()
        {
            var method = typeof(FeedController).GetMethod("DrawNext", AnyInstance);
            Assert.IsNotNull(method, "FeedController.DrawNext not found");
            return (EncounterData)method.Invoke(_feed, null);
        }

        private int GetBagCount()
        {
            return ((List<int>)GetField(_feed, "_shuffleBag")).Count;
        }

        private void SetAtFloor(bool atFloor)
        {
            _health.LoadState(FullHp, atFloor ? FloorHp : FullHp, _health.LastRegenUtcTicks);
        }

        // Each entry gets a unique title so tests can tell two same-typed entries apart.
        private void SetPool(params EncounterType[] types)
        {
            var pool = new EncounterData[types.Length];
            for (var i = 0; i < types.Length; i++)
            {
                var data = ScriptableObject.CreateInstance<EncounterData>();
                data.type = types[i];
                data.title = $"entry_{i}";
                pool[i] = data;
            }

            SetField(_feed, "_pool", pool);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, AnyInstance);
            Assert.IsNotNull(field, $"field '{name}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, AnyInstance);
            Assert.IsNotNull(field, $"field '{name}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}
