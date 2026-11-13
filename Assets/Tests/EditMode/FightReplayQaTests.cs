using System;
using System.IO;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA coverage for docs/tasks/009-replay-precomputed-fight.md criteria not covered by
    // FightReplayTests: Revive HP parity with the simulator, a seeded sweep of full fights
    // (per-beat HP invariants + final HP match), ResolveStep's room handling, and source
    // checks that the views replay the result and spend the Revive only when it fired.
    public class FightReplayQaTests
    {
        private static string ReadSource(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relative));
        }

        private static FightInput Input(int seed, int playerStartHp, int enemyMaxHp, int enemyDamage,
            float enemySpeed, int revivePercent)
        {
            return new FightInput
            {
                seed = seed,
                reviveArmed = revivePercent > 0,
                revivePercent = revivePercent,
                player = new PlayerSideInput { startHp = playerStartHp },
                enemy = new EnemySideInput { maxHp = enemyMaxHp, attackDamage = enemyDamage, attackSpeed = enemySpeed }
            };
        }

        // Criterion 2: the Revive HP the view shows must equal the simulator's own Revive HP.
        [Test]
        public void ReviveHp_MatchesSimulatorForRangeOfPercentsAndStartHp()
        {
            var checkedCount = 0;
            foreach (var startHp in new[] { 1, 7, 20, 33, 100, 251 })
            {
                foreach (var percent in new[] { 1, 10, 33, 50, 99, 100 })
                {
                    // FixedRandom: enemy never misses and damage is exact, so the player always dies.
                    var input = Input(1, startHp, 100000, 1000, 2f, percent);
                    var sim = new CombatSimulator(input, new FixedRandom());
                    sim.RunUntilDeathOrVictory();

                    Assert.IsTrue(sim.ReviveFired, $"precondition: revive fires (start {startHp}, {percent}%)");
                    Assert.AreEqual(sim.ReviveHp, FightReplay.ReviveHp(startHp, percent),
                        $"start {startHp}, {percent}%");
                    checkedCount++;
                }
            }

            Assert.AreEqual(36, checkedCount);
        }

        // Criterion 5: across many seeded fights (wins and losses, with and without a Revive)
        // the precomputed timeline stays consistent beat by beat and lands on the result's final HP.
        [Test]
        public void Timeline_SeededSweep_MatchesFinalHpAndHoldsPerBeatInvariants()
        {
            var revivedFights = 0;
            var wins = 0;
            var losses = 0;

            for (var seed = 1; seed <= 200; seed++)
            {
                // Cycle through: a 1-HP enemy (win, no Revive armed), a fight the Revive
                // carries and then loses, and a fight with a Revive armed that is won.
                var input = (seed % 3) switch
                {
                    0 => Input(seed, 40, 1, 3, 1f, 0),
                    1 => Input(seed, 15, 500, 12, 2f, 30),
                    _ => Input(seed, 40, 1, 3, 1f, 60),
                };
                var result = CombatSimulator.Resolve(input);
                var reviveHp = FightReplay.ReviveHp(input.player.startHp, input.revivePercent);
                var timeline = FightReplay.BuildHpTimeline(result, input.player.startHp, input.enemy.maxHp, reviveHp);

                Assert.AreEqual(result.beats.Length, timeline.Length, $"seed {seed}: one entry per beat");
                if (timeline.Length == 0)
                {
                    continue;
                }

                if (result.outcome == FightOutcome.Win) wins++; else losses++;

                var prevPlayer = input.player.startHp;
                var prevEnemy = input.enemy.maxHp;
                var revivedFlags = 0;

                for (var i = 0; i < timeline.Length; i++)
                {
                    var beat = result.beats[i];
                    var t = timeline[i];

                    Assert.GreaterOrEqual(t.PlayerHp, 0, $"seed {seed} beat {i}: player HP floored at 0");
                    Assert.GreaterOrEqual(t.EnemyHp, 0, $"seed {seed} beat {i}: enemy HP floored at 0");
                    Assert.LessOrEqual(t.EnemyHp, prevEnemy, $"seed {seed} beat {i}: enemy HP never rises");
                    Assert.LessOrEqual(t.PlayerHp, prevPlayer, $"seed {seed} beat {i}: player HP only drops before a revive");

                    if (beat.WasMiss)
                    {
                        Assert.AreEqual(prevEnemy, t.EnemyHp, $"seed {seed} beat {i}: a miss changes no HP");
                    }

                    if (t.Revived)
                    {
                        revivedFlags++;
                        Assert.AreEqual(i, result.reviveBeatIndex, $"seed {seed}: revive flagged at the result's beat");
                        Assert.AreEqual(reviveHp, t.PlayerHpAfterRevive, $"seed {seed}: revive restores ReviveHp");
                        Assert.LessOrEqual(t.PlayerHpAfterRevive, input.player.startHp);
                    }
                    else
                    {
                        Assert.AreEqual(t.PlayerHp, t.PlayerHpAfterRevive, $"seed {seed} beat {i}: no revive, no jump");
                    }

                    prevPlayer = t.PlayerHpAfterRevive;
                    prevEnemy = t.EnemyHp;
                }

                Assert.AreEqual(result.reviveFired ? 1 : 0, revivedFlags, $"seed {seed}: revive flagged exactly when fired");
                if (result.reviveFired)
                {
                    revivedFights++;
                }

                var last = timeline[timeline.Length - 1];
                Assert.AreEqual(result.finalPlayerHp, last.PlayerHpAfterRevive, $"seed {seed}: final player HP");
                Assert.AreEqual(result.finalEnemyHp, last.EnemyHp, $"seed {seed}: final enemy HP");
                Assert.AreEqual(result.outcome == FightOutcome.Win, last.EnemyHp == 0,
                    $"seed {seed}: win iff enemy at 0");
            }

            // The sweep must actually exercise both outcomes and the Revive path.
            Assert.Greater(revivedFights, 0, "sweep should include fights where the Revive fired");
            Assert.Greater(wins, 0, "sweep should include wins");
            Assert.Greater(losses, 0, "sweep should include losses");
        }

        // Criterion 4: ResolveStep resolves a fight room from the step's own stats and returns
        // null for rooms that are not fights.
        [Test]
        public void ResolveStep_FightRoomResolvesFromStepStats_NonFightRoomsAreNull()
        {
            var loadout = TestFixtures.MakeLoadout();
            var attributes = new PlayerAttributes();

            var fight = MakeStep(isRest: false, isBonus: false, isFinal: false, enemyHealth: 35);
            var resolved = CombatResolver.ResolveStep(loadout, attributes, null, fight, 22, new System.Random(5));
            Assert.IsNotNull(resolved);
            Assert.AreEqual(35, resolved.Input.enemy.maxHp);
            Assert.AreEqual(22, resolved.Input.player.startHp);
            Assert.IsNotNull(resolved.Result);
            Assert.IsNotNull(resolved.Result.beats);

            Assert.IsNull(CombatResolver.ResolveStep(loadout, attributes, null,
                MakeStep(isRest: true, isBonus: false, isFinal: false, enemyHealth: 35), 22, new System.Random(5)));
            Assert.IsNull(CombatResolver.ResolveStep(loadout, attributes, null,
                MakeStep(isRest: false, isBonus: true, isFinal: false, enemyHealth: 35), 22, new System.Random(5)));
            Assert.IsNull(CombatResolver.ResolveStep(loadout, attributes, null,
                MakeStep(isRest: false, isBonus: false, isFinal: true, enemyHealth: 35), 22, new System.Random(5)));
        }

        [Test]
        public void ResolveStep_SameSeedGivesSameResult()
        {
            var loadout = TestFixtures.MakeLoadout();
            var step = MakeStep(isRest: false, isBonus: false, isFinal: false, enemyHealth: 60);

            var a = CombatResolver.ResolveStep(loadout, new PlayerAttributes(), null, step, 30, new System.Random(99));
            var b = CombatResolver.ResolveStep(loadout, new PlayerAttributes(), null, step, 30, new System.Random(99));

            Assert.AreEqual(a.Input.seed, b.Input.seed);
            Assert.AreEqual(a.Result.outcome, b.Result.outcome);
            Assert.AreEqual(a.Result.finalPlayerHp, b.Result.finalPlayerHp);
            Assert.AreEqual(a.Result.beats.Length, b.Result.beats.Length);
        }

        // Criterion 2 (view side): the carried Revive is spent only when the result says it fired.
        // The view is a MonoBehaviour, so this checks its source directly.
        [Test]
        public void PlaybackView_SpendsReviveOnlyInsideFiredGuard()
        {
            var source = ReadSource("Scripts/Combat/CombatPlaybackView.cs");
            var playStart = source.IndexOf("public IEnumerator Play(FightResult", StringComparison.Ordinal);
            var playEnd = source.IndexOf("// The resource-node minigame's entry point", StringComparison.Ordinal);
            Assert.That(playStart, Is.GreaterThanOrEqualTo(0), "Play(FightResult ...) should exist");
            Assert.That(playEnd, Is.GreaterThan(playStart));

            var play = source.Substring(playStart, playEnd - playStart);
            StringAssert.DoesNotContain("CombatSimulator", play);
            StringAssert.DoesNotContain("RunUntilDeathOrVictory", play);
            StringAssert.DoesNotContain("CastManualAbility", play);
            StringAssert.DoesNotContain("ReviveFired", play);

            var guard = play.IndexOf("if (result.reviveFired)", StringComparison.Ordinal);
            var consume = play.IndexOf("ConsumeRevivePotion", StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThanOrEqualTo(0), "Play should guard the Revive spend on result.reviveFired");
            Assert.That(consume, Is.GreaterThan(guard), "ConsumeRevivePotion should run after the fired guard");
        }

        // Criterion 3: outcome and close-call flags come from the result.
        [Test]
        public void PlaybackView_OutcomeAndCloseCallComeFromResult()
        {
            var source = ReadSource("Scripts/Combat/CombatPlaybackView.cs");
            StringAssert.Contains("LastOutcomeWasVictory = result.outcome == FightOutcome.Win;", source);
            StringAssert.Contains("WasCloseCall = FightReplay.IsCloseCall(result, playerStartHp);", source);
            StringAssert.Contains("FinalPlayerHp = result.finalPlayerHp;", source);
        }

        // Criterion 4: both callers resolve via CombatResolver and pass the result to Play.
        [Test]
        public void Callers_ResolveViaCombatResolverAndPassResultToPlay()
        {
            var encounter = ReadSource("Scripts/Encounters/CombatResolutionView.cs");
            StringAssert.Contains("CombatResolver.ResolveEncounter(", encounter);
            StringAssert.Contains("_playback.Play(fight.Result, startHp", encounter);
            StringAssert.DoesNotContain("CombatResolver.CreateSimulator(", encounter);

            var dungeon = ReadSource("Scripts/Encounters/DungeonStepPageView.cs");
            StringAssert.Contains("CombatResolver.ResolveStep(", dungeon);
            StringAssert.Contains("_playback.Play(fight.Result, startHp", dungeon);
            StringAssert.DoesNotContain("CreateSimulatorForStep(", dungeon);
        }

        // Criterion 1: the playback no longer runs the live simulator. Covered for the
        // Play method body (PlayTimedWindow still takes a sim for resource nodes).
        [Test]
        public void PlaybackView_PlayTakesResultAndDisplayInfo()
        {
            var source = ReadSource("Scripts/Combat/CombatPlaybackView.cs");
            StringAssert.Contains("public IEnumerator Play(FightResult result, int playerStartHp, int enemyMaxHp, int reviveHp,", source);
        }

        private static DungeonStep MakeStep(bool isRest, bool isBonus, bool isFinal, int enemyHealth)
        {
            return new DungeonStep(depthIndex: 0, label: "Room", isRestRoom: isRest, isBonusTreasureRoom: isBonus,
                isFinalTreasureRoom: isFinal, xpReward: 0, goldReward: 0, rewardItems: null, enemyHealth: enemyHealth,
                enemyAttackDamage: 3, enemyAttackSpeed: 1f, enemyWeaknesses: null, enemySprite: null, stepColor: default);
        }
    }
}
