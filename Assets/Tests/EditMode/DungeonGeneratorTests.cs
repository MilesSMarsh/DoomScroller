using System.Linq;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class DungeonGeneratorTests
    {
        private static EncounterData MakeDungeon(params DungeonRoomData[] rooms)
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.type = EncounterType.Dungeon;
            data.baseXpReward = 100;
            data.goldReward = 0;
            data.dungeonRooms = rooms;
            return data;
        }

        private static DungeonRoomData MakeFight(string label, int health, int damage, float speed, float appearChance = 100f)
        {
            return new DungeonRoomData
            {
                label = label, type = DungeonRoomType.Fight, appearChance = appearChance,
                enemyHealth = health, enemyAttackDamage = damage, enemyAttackSpeed = speed
            };
        }

        [Test]
        public void Generate_RoomAtOneHundredPercent_AlwaysAppears()
        {
            var data = MakeDungeon(MakeFight("Goblin", 50, 5, 1f, appearChance: 100f));

            for (var seed = 0; seed < 20; seed++)
            {
                var steps = DungeonGenerator.Generate(data, new System.Random(seed));
                Assert.AreEqual(2, steps.Count, "1 authored fight + 1 guaranteed final room");
            }
        }

        [Test]
        public void Generate_RoomAtZeroPercent_NeverAppears()
        {
            var data = MakeDungeon(MakeFight("Goblin", 50, 5, 1f, appearChance: 0f));

            for (var seed = 0; seed < 20; seed++)
            {
                var steps = DungeonGenerator.Generate(data, new System.Random(seed));
                Assert.AreEqual(1, steps.Count, "the fight never rolls in; only the guaranteed final room remains");
                Assert.IsTrue(steps[0].IsFinalTreasureRoom);
            }
        }

        [Test]
        public void Generate_RoomAppearChance_MatchesOverManyTrials()
        {
            var data = MakeDungeon(MakeFight("Goblin", 50, 5, 1f, appearChance: 30f));
            var rng = new System.Random(12345);
            const int trials = 5000;
            var appearances = 0;
            for (var i = 0; i < trials; i++)
            {
                if (DungeonGenerator.Generate(data, rng).Count == 2)
                {
                    appearances++;
                }
            }

            Assert.AreEqual(0.30f, appearances / (float)trials, 0.03f);
        }

        [Test]
        public void Generate_PreservesAuthoredRoomOrderAndStats()
        {
            var data = MakeDungeon(
                MakeFight("First", 40, 4, 0.5f),
                MakeFight("Second", 90, 12, 1.2f));

            var steps = DungeonGenerator.Generate(data, new System.Random(1));

            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual("First", steps[0].Label);
            Assert.AreEqual(40, steps[0].EnemyHealth);
            Assert.AreEqual(4, steps[0].EnemyAttackDamage);
            Assert.AreEqual(0.5f, steps[0].EnemyAttackSpeed);

            Assert.AreEqual("Second", steps[1].Label);
            Assert.AreEqual(90, steps[1].EnemyHealth);
            Assert.AreEqual(12, steps[1].EnemyAttackDamage);
            Assert.AreEqual(1.2f, steps[1].EnemyAttackSpeed);

            Assert.IsTrue(steps[2].IsFinalTreasureRoom);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, steps.Select(s => s.DepthIndex));
        }

        [Test]
        public void Generate_EachFightRoom_CarriesItsOwnWeaknessesAndSprite_NotSharedAcrossTheDungeon()
        {
            var sprite1 = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);
            var sprite2 = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);
            var weaknessesA = new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 2f } };
            var weaknessesB = new[] { new DamageTypeModifier { type = DamageType.Arcane, multiplier = 0.5f } };

            var roomA = MakeFight("First", 40, 4, 0.5f);
            roomA.enemySprite = sprite1;
            roomA.damageTypeModifiers = weaknessesA;

            var roomB = MakeFight("Second", 90, 12, 1.2f);
            roomB.enemySprite = sprite2;
            roomB.damageTypeModifiers = weaknessesB;

            var data = MakeDungeon(roomA, roomB);
            var steps = DungeonGenerator.Generate(data, new System.Random(1));

            Assert.AreSame(sprite1, steps[0].EnemySprite);
            Assert.AreSame(weaknessesA, steps[0].EnemyWeaknesses);
            Assert.AreSame(sprite2, steps[1].EnemySprite);
            Assert.AreSame(weaknessesB, steps[1].EnemyWeaknesses);
        }

        [Test]
        public void Generate_FinalRoomAlwaysPresentAndLast_EvenWithNoAuthoredRooms()
        {
            var data = MakeDungeon();

            var steps = DungeonGenerator.Generate(data, new System.Random(1));

            Assert.AreEqual(1, steps.Count);
            Assert.IsTrue(steps[0].IsFinalTreasureRoom);
            Assert.IsFalse(steps[0].IsBonusTreasureRoom);
            Assert.IsFalse(steps[0].IsRestRoom);
        }

        [Test]
        public void Generate_FightRoom_GrantsNoRewardFields()
        {
            var data = MakeDungeon(MakeFight("Goblin", 50, 5, 1f));

            var steps = DungeonGenerator.Generate(data, new System.Random(1));
            var fightStep = steps[0];

            Assert.IsFalse(fightStep.IsFinalTreasureRoom);
            Assert.IsFalse(fightStep.IsBonusTreasureRoom);
            Assert.IsFalse(fightStep.IsRestRoom);
            Assert.AreEqual(0, fightStep.XpReward);
            Assert.AreEqual(0, fightStep.GoldReward);
        }

        [Test]
        public void Generate_RestRoom_HasNoRewardOrEnemyStats()
        {
            var data = MakeDungeon(new DungeonRoomData { type = DungeonRoomType.Rest, appearChance = 100f });

            var steps = DungeonGenerator.Generate(data, new System.Random(1));
            var restStep = steps[0];

            Assert.IsTrue(restStep.IsRestRoom);
            Assert.IsFalse(restStep.IsFinalTreasureRoom);
            Assert.IsFalse(restStep.IsBonusTreasureRoom);
            Assert.AreEqual(0, restStep.XpReward);
            Assert.AreEqual(0, restStep.GoldReward);
        }

        [Test]
        public void Generate_BonusTreasureRoom_GrantsItsOwnReward_AndNeverEndsTheRun()
        {
            var bonusTable = new float[5];
            bonusTable[(int)ItemRarity.Legendary] = 1f;
            var data = MakeDungeon(new DungeonRoomData
            {
                type = DungeonRoomType.Treasure, appearChance = 100f, goldReward = 42,
                lootTable = new LootTable { percentByRarity = bonusTable }
            });
            // Final room's own table is all-Common (default) — distinct from the bonus room's.
            data.goldReward = 0;

            var steps = DungeonGenerator.Generate(data, new System.Random(1));

            Assert.AreEqual(2, steps.Count);
            var bonusStep = steps[0];
            Assert.IsTrue(bonusStep.IsBonusTreasureRoom);
            Assert.IsFalse(bonusStep.IsFinalTreasureRoom);
            Assert.AreEqual(0, bonusStep.XpReward, "a bonus pickup never grants XP");
            Assert.AreEqual(42, bonusStep.GoldReward);
            Assert.AreEqual(1, bonusStep.RewardItems.Length);
            Assert.AreEqual(ItemRarity.Legendary, bonusStep.RewardItems[0].rarity);

            var finalStep = steps[1];
            Assert.IsTrue(finalStep.IsFinalTreasureRoom);
            Assert.AreEqual(100, finalStep.XpReward, "the guaranteed reward uses the dungeon's own baseXpReward");
            Assert.AreEqual(1, finalStep.RewardItems.Length);
            Assert.AreEqual(ItemRarity.Common, finalStep.RewardItems[0].rarity, "the final room rolls from its own separate table");
        }

        [Test]
        public void Generate_FinalRoom_UsesAuthoredGoldReward_NotDerivedFromXp()
        {
            var data = MakeDungeon();
            data.goldReward = 250;

            var steps = DungeonGenerator.Generate(data, new System.Random(1));

            Assert.AreEqual(250, steps[0].GoldReward);
        }
    }
}
