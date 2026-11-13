using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Feed;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class FightReplayTests
    {
        private static CombatBeat Hit(CombatActor actor, int damage) => new CombatBeat(actor, false, false, damage);
        private static CombatBeat Miss(CombatActor actor) => new CombatBeat(actor, false, true, 0);

        private static FightInput ReviveInput(int seed)
        {
            // Enemy one-shots the 20 HP player at its first hit, long before the 60 HP enemy
            // could be worn down by an unarmed player, so the armed Revive must fire.
            return new FightInput
            {
                seed = seed,
                reviveArmed = true,
                revivePercent = 50,
                player = new PlayerSideInput { startHp = 20 },
                enemy = new EnemySideInput { maxHp = 60, attackDamage = 30, attackSpeed = 2f }
            };
        }

        [Test]
        public void Timeline_TracksBothSidesAndIgnoresMisses()
        {
            var result = new FightResult
            {
                outcome = FightOutcome.Win,
                beats = new[]
                {
                    Hit(CombatActor.Player, 30), Hit(CombatActor.Enemy, 10), Miss(CombatActor.Enemy),
                    Miss(CombatActor.Player), Hit(CombatActor.Player, 100)
                }
            };

            var t = FightReplay.BuildHpTimeline(result, 50, 100, 0);

            Assert.AreEqual(new[] { 50, 40, 40, 40, 40 }, System.Array.ConvertAll(t, b => b.PlayerHp));
            Assert.AreEqual(new[] { 70, 70, 70, 70, 0 }, System.Array.ConvertAll(t, b => b.EnemyHp));
            Assert.IsTrue(System.Array.TrueForAll(t, b => !b.Revived));
            Assert.IsTrue(System.Array.TrueForAll(t, b => b.PlayerHpAfterRevive == b.PlayerHp));
        }

        [Test]
        public void Timeline_EmptyOrNullBeatsGiveEmptyTimeline()
        {
            Assert.AreEqual(0, FightReplay.BuildHpTimeline(new FightResult { beats = null }, 10, 10, 1).Length);
            Assert.AreEqual(0, FightReplay.BuildHpTimeline(new FightResult(), 10, 10, 1).Length);
        }

        [Test]
        public void Timeline_OverkillFloorsAtZeroNotNegative()
        {
            var result = new FightResult
            {
                outcome = FightOutcome.Win,
                beats = new[] { Hit(CombatActor.Player, 500), Hit(CombatActor.Enemy, 500) }
            };

            var t = FightReplay.BuildHpTimeline(result, 30, 40, 0);

            Assert.AreEqual(0, t[0].EnemyHp);
            Assert.AreEqual(0, t[1].PlayerHp);
        }

        [Test]
        public void Timeline_RevivePutsRestoredHpFromTheReviveBeatOn()
        {
            var result = new FightResult
            {
                outcome = FightOutcome.Win,
                reviveFired = true,
                reviveBeatIndex = 1,
                beats = new[]
                {
                    Hit(CombatActor.Enemy, 20), Hit(CombatActor.Enemy, 50), Hit(CombatActor.Enemy, 5),
                    Hit(CombatActor.Player, 100)
                }
            };

            var t = FightReplay.BuildHpTimeline(result, 60, 100, 30);

            Assert.AreEqual(40, t[0].PlayerHp);
            Assert.AreEqual(0, t[1].PlayerHp);
            Assert.IsTrue(t[1].Revived);
            Assert.AreEqual(30, t[1].PlayerHpAfterRevive);
            Assert.AreEqual(25, t[2].PlayerHp);
            Assert.AreEqual(25, t[2].PlayerHpAfterRevive);
            Assert.IsFalse(t[2].Revived);
            Assert.AreEqual(0, t[3].EnemyHp);
        }

        [Test]
        public void Timeline_ReviveIgnoredWhenResultSaysItDidNotFire()
        {
            var result = new FightResult { reviveFired = false, reviveBeatIndex = 0, beats = new[] { Hit(CombatActor.Enemy, 99) } };
            var t = FightReplay.BuildHpTimeline(result, 50, 10, 25);
            Assert.IsFalse(t[0].Revived);
            Assert.AreEqual(0, t[0].PlayerHp);
        }

        [Test]
        public void Timeline_ReviveAtBeatIndexMinusOneNeverFires()
        {
            var result = new FightResult { reviveFired = true, reviveBeatIndex = -1, beats = new[] { Hit(CombatActor.Enemy, 5) } };
            var t = FightReplay.BuildHpTimeline(result, 50, 10, 25);
            Assert.IsFalse(t[0].Revived);
        }

        [Test]
        public void Timeline_SimulatorRevive_ShowsReviveAtItsBeatAndMatchesFinalHp()
        {
            var result = CombatSimulator.Resolve(ReviveInput(seed: 7));
            Assert.IsTrue(result.reviveFired, "precondition: the armed Revive should fire in this fight");

            var reviveHp = FightReplay.ReviveHp(20, 50);
            var t = FightReplay.BuildHpTimeline(result, 20, 60, reviveHp);

            Assert.AreEqual(10, reviveHp);
            Assert.AreEqual(result.beats.Length, t.Length);
            Assert.IsTrue(t[result.reviveBeatIndex].Revived);
            Assert.AreEqual(0, t[result.reviveBeatIndex].PlayerHp);
            Assert.AreEqual(reviveHp, t[result.reviveBeatIndex].PlayerHpAfterRevive);

            for (var i = 0; i < t.Length; i++)
            {
                if (i != result.reviveBeatIndex)
                {
                    Assert.IsFalse(t[i].Revived, $"only the revive beat should be flagged (beat {i})");
                }
            }

            var last = t[t.Length - 1];
            Assert.AreEqual(result.finalPlayerHp, last.PlayerHpAfterRevive);
            Assert.AreEqual(result.finalEnemyHp, last.EnemyHp);
        }

        [Test]
        public void Timeline_SimulatorNoRevive_FinalHpMatchesResultForWinAndLoss()
        {
            // Strong player vs weak enemy: a win with no Revive.
            var win = CombatSimulator.Resolve(new FightInput
            {
                seed = 3,
                player = new PlayerSideInput { startHp = 500 },
                enemy = new EnemySideInput { maxHp = 40, attackDamage = 1, attackSpeed = 1f }
            });
            Assert.AreEqual(FightOutcome.Win, win.outcome);
            Assert.IsFalse(win.reviveFired);
            var winTimeline = FightReplay.BuildHpTimeline(win, 500, 40, 1);
            Assert.AreEqual(win.beats.Length, winTimeline.Length);
            Assert.AreEqual(win.finalPlayerHp, winTimeline[winTimeline.Length - 1].PlayerHp);
            Assert.AreEqual(win.finalEnemyHp, winTimeline[winTimeline.Length - 1].EnemyHp);

            // Weak player vs strong enemy, no Revive armed: a loss.
            var loss = CombatSimulator.Resolve(new FightInput
            {
                seed = 3,
                player = new PlayerSideInput { startHp = 10 },
                enemy = new EnemySideInput { maxHp = 5000, attackDamage = 50, attackSpeed = 2f }
            });
            Assert.AreEqual(FightOutcome.Loss, loss.outcome);
            var lossTimeline = FightReplay.BuildHpTimeline(loss, 10, 5000, 1);
            Assert.AreEqual(loss.finalPlayerHp, lossTimeline[lossTimeline.Length - 1].PlayerHp);
            Assert.AreEqual(loss.finalEnemyHp, lossTimeline[lossTimeline.Length - 1].EnemyHp);
        }

        [Test]
        public void ReviveHp_RoundsUpAndNeverBelowOne()
        {
            Assert.AreEqual(4, FightReplay.ReviveHp(10, 33));
            Assert.AreEqual(20, FightReplay.ReviveHp(20, 100));
            Assert.AreEqual(1, FightReplay.ReviveHp(0, 50));
            Assert.AreEqual(1, FightReplay.ReviveHp(5, 0));
        }

        [Test]
        public void CloseCall_OnlyForLowHpWins()
        {
            Assert.IsTrue(FightReplay.IsCloseCall(new FightResult { outcome = FightOutcome.Win, finalPlayerHp = 4 }, 20));
            // Exactly 25% is not a close call (strictly below).
            Assert.IsFalse(FightReplay.IsCloseCall(new FightResult { outcome = FightOutcome.Win, finalPlayerHp = 5 }, 20));
            Assert.IsFalse(FightReplay.IsCloseCall(new FightResult { outcome = FightOutcome.Loss, finalPlayerHp = 0 }, 20));
            Assert.IsFalse(FightReplay.IsCloseCall(new FightResult { outcome = FightOutcome.Win, finalPlayerHp = 0 }, 0));
        }

        [Test]
        public void ResolveEncounter_TreasureIsNullAndEnemyResolvesDeterministically()
        {
            var loadout = TestFixtures.MakeLoadout();
            var attributes = new PlayerAttributes();

            var treasure = ScriptableObject.CreateInstance<EncounterData>();
            treasure.type = EncounterType.Treasure;
            Assert.IsNull(CombatResolver.ResolveEncounter(loadout, attributes, null, treasure, 50));

            var enemy = ScriptableObject.CreateInstance<EncounterData>();
            enemy.type = EncounterType.Enemy;
            enemy.enemyHealth = 30;
            enemy.enemyAttackDamage = 4;
            enemy.enemyAttackSpeed = 1f;

            var a = CombatResolver.ResolveEncounter(loadout, attributes, null, enemy, 25, new System.Random(42));
            var b = CombatResolver.ResolveEncounter(loadout, attributes, null, enemy, 25, new System.Random(42));

            Assert.IsNotNull(a);
            Assert.AreEqual(30, a.Input.enemy.maxHp);
            Assert.AreEqual(25, a.Input.player.startHp);
            Assert.AreEqual(a.Input.seed, b.Input.seed);
            Assert.AreEqual(a.Result.outcome, b.Result.outcome);
            Assert.AreEqual(a.Result.finalPlayerHp, b.Result.finalPlayerHp);
            Assert.AreEqual(a.Result.beats.Length, b.Result.beats.Length);
        }

        // The playback view must replay a result, not drive the live simulator.
        [Test]
        public void PlaybackView_NoLongerDrivesTheLiveSimulator()
        {
            var path = System.IO.Path.Combine(Application.dataPath, "Scripts/Combat/CombatPlaybackView.cs");
            var source = System.IO.File.ReadAllText(path);

            StringAssert.DoesNotContain("RunUntilDeathOrVictory", source);
            StringAssert.DoesNotContain("CastManualAbility", source);
        }
    }
}
