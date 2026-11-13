using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA checks for spec 008 that CombatCoreAcceptanceTests / CombatCoreContractTests /
    // CombatCoreAdapterTests don't reach: invariants that tie FightResult's outcome, final
    // HP and beat list together across many seeds, the Revive beat's position, degenerate
    // and zero-HP inputs, the CombatCore namespace, and the playback view's revive prompt.
    public class CombatCoreQaTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 7, 42, 99, 1234, 9001, 99991, 123456 };

        // Both sides can act and the fight usually resolves well before the beat budget.
        private static FightInput MakeFight(int seed, int playerHp = 40, int enemyHp = 60, int enemyDamage = 7)
        {
            return new FightInput
            {
                seed = seed,
                player = new PlayerSideInput
                {
                    startHp = playerHp,
                    defense = 1,
                    weapons = new[] { new WeaponInput { power = 6, attackSpeed = 1.1f, damageType = 0 } },
                    abilities = new[]
                    {
                        new AbilityInput { name = "Jab", weaponIndex = 0, powerBonus = 3, cooldownSeconds = 2f }
                    }
                },
                enemy = new EnemySideInput { maxHp = enemyHp, attackDamage = enemyDamage, attackSpeed = 0.9f }
            };
        }

        private static FightInput MakeArmedFight(int seed, int playerHp, int enemyHp, int enemyDamage, int revivePercent)
        {
            var input = MakeFight(seed, playerHp, enemyHp, enemyDamage);
            input.reviveArmed = true;
            input.revivePercent = revivePercent;
            return input;
        }

        // Invariant: the final HP of each side equals its start HP minus everything the
        // other side landed, floored at 0. Outcome follows from who is left standing.
        [Test]
        public void Result_FinalHpAndOutcomeAgreeWithTheBeatList_WithoutRevive()
        {
            foreach (var seed in Seeds)
            {
                var input = MakeFight(seed);
                var result = CombatSimulator.Resolve(input);

                var damageToEnemy = result.beats.Where(b => b.Actor == CombatActor.Player && !b.WasMiss)
                    .Sum(b => b.DisplayDamage);
                var damageToPlayer = result.beats.Where(b => b.Actor == CombatActor.Enemy && !b.WasMiss)
                    .Sum(b => b.DisplayDamage);

                Assert.AreEqual(Math.Max(0, input.enemy.maxHp - damageToEnemy), result.finalEnemyHp,
                    "seed " + seed + ": final enemy HP");
                Assert.AreEqual(Math.Max(0, input.player.startHp - damageToPlayer), result.finalPlayerHp,
                    "seed " + seed + ": final player HP");
                Assert.IsFalse(result.reviveFired, "seed " + seed + ": nothing armed, nothing fires");
                Assert.AreEqual(-1, result.reviveBeatIndex, "seed " + seed);

                // Exactly one side is down when the fight ends.
                Assert.IsTrue((result.finalPlayerHp <= 0) != (result.finalEnemyHp <= 0),
                    "seed " + seed + ": exactly one side must be dead");
                var expected = result.finalEnemyHp <= 0 ? FightOutcome.Win : FightOutcome.Loss;
                Assert.AreEqual(expected, result.outcome, "seed " + seed + ": outcome");
            }
        }

        // Invariant: the Revive beat is the enemy hit that took the player to 0, the fight
        // carries on after it, and a second death ends the fight.
        [Test]
        public void ArmedRevive_BeatIndexIsThePlayerKillingHit_AndTheFightCarriesOn()
        {
            var fired = 0;
            foreach (var seed in Seeds)
            {
                var input = MakeArmedFight(seed, playerHp: 20, enemyHp: 100000, enemyDamage: 30, revivePercent: 50);
                var result = CombatSimulator.Resolve(input);

                Assert.IsTrue(result.reviveFired, "seed " + seed + ": a 20 HP player vs 30 damage hits must need the Revive");
                fired++;

                var idx = result.reviveBeatIndex;
                Assert.GreaterOrEqual(idx, 0, "seed " + seed);
                Assert.Less(idx, result.beats.Length - 1, "seed " + seed + ": beats must continue after the Revive");
                Assert.AreEqual(CombatActor.Enemy, result.beats[idx].Actor, "seed " + seed + ": the killing hit is the enemy's");
                Assert.IsFalse(result.beats[idx].WasMiss, "seed " + seed + ": a miss can't kill");

                // Replay the pre-Revive damage: HP stays above 0 until the Revive beat.
                var hp = input.player.startHp;
                for (var i = 0; i < idx; i++)
                {
                    if (result.beats[i].Actor == CombatActor.Enemy && !result.beats[i].WasMiss)
                    {
                        hp -= result.beats[i].DisplayDamage;
                    }
                }
                Assert.Greater(hp, 0, "seed " + seed + ": player died before the Revive beat");
                hp -= result.beats[idx].DisplayDamage;
                Assert.LessOrEqual(hp, 0, "seed " + seed + ": the Revive beat must be the killing hit");

                // Revive heals 50% of 20 = 10, and the second death ends the fight.
                Assert.AreEqual(FightOutcome.Loss, result.outcome, "seed " + seed);
                Assert.AreEqual(0, result.finalPlayerHp, "seed " + seed);
            }

            Assert.AreEqual(Seeds.Length, fired);
        }

        // Invariant: the Revive only fires when the player would actually die. A player who
        // wins outright keeps the potion's effect unused.
        [Test]
        public void ArmedRevive_NeverFires_WhenThePlayerWinsOutright()
        {
            foreach (var seed in Seeds)
            {
                var input = MakeArmedFight(seed, playerHp: 200, enemyHp: 20, enemyDamage: 1, revivePercent: 100);
                input.player.weapons[0].power = 30;
                var result = CombatSimulator.Resolve(input);

                Assert.AreEqual(FightOutcome.Win, result.outcome, "seed " + seed);
                Assert.IsFalse(result.reviveFired, "seed " + seed);
                Assert.AreEqual(-1, result.reviveBeatIndex, "seed " + seed);
            }
        }

        // Edge: a degenerate fight (no damage, no speed, huge enemy pool) must still end
        // inside the beat budget with exactly one side down.
        [Test]
        public void DegenerateFight_TerminatesWithinTheBeatBudget_AndOneSideIsDown()
        {
            foreach (var seed in Seeds)
            {
                var input = new FightInput
                {
                    seed = seed,
                    player = new PlayerSideInput { startHp = 40 },
                    enemy = new EnemySideInput { maxHp = 1000000000, attackDamage = 0, attackSpeed = 0f }
                };

                var result = CombatSimulator.Resolve(input);

                Assert.LessOrEqual(result.beats.Length, 200, "seed " + seed + ": MaxBeats budget exceeded");
                Assert.IsTrue((result.finalPlayerHp <= 0) != (result.finalEnemyHp <= 0),
                    "seed " + seed + ": the safety cap must force exactly one side down");
            }
        }

        // Edge: an enemy with no HP is already dead — resolves as a win with no beats.
        [Test]
        public void ZeroHpEnemy_ResolvesAsAWinWithNoBeats()
        {
            var input = MakeFight(seed: 5, enemyHp: 0);
            FightResult result = null;

            Assert.DoesNotThrow(() => result = CombatSimulator.Resolve(input));
            Assert.AreEqual(FightOutcome.Win, result.outcome);
            Assert.IsEmpty(result.beats);
            Assert.AreEqual(0, result.finalEnemyHp);
        }

        // Edge: a zero or negative start HP is floored at 1 (the sim never starts dead).
        // A resource-style enemy (canAttack false) lets the fight run to a clean win.
        [Test]
        public void ZeroStartHp_IsFlooredAtOne()
        {
            var input = MakeFight(seed: 9, playerHp: 0, enemyHp: 10);
            input.enemy.canAttack = false;

            var result = CombatSimulator.Resolve(input);

            Assert.AreEqual(1, result.finalPlayerHp);
            Assert.AreEqual(FightOutcome.Win, result.outcome);
            Assert.IsTrue(result.beats.All(b => b.Actor == CombatActor.Player), "a passive enemy never swings");
        }

        // Criterion 1 detail: every CombatCore file declares the CombatCore namespace.
        [Test]
        public void CombatCore_EveryFileDeclaresTheCombatCoreNamespace()
        {
            var folder = Path.Combine(Application.dataPath, "Scripts", "CombatCore");
            Assert.IsTrue(Directory.Exists(folder), "missing " + folder);

            var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            var offenders = files
                .Where(f => !File.ReadAllText(f).Contains("namespace DoomScroller.CombatCore"))
                .Select(Path.GetFileName)
                .ToArray();
            Assert.IsEmpty(offenders, "files outside DoomScroller.CombatCore: " + string.Join(", ", offenders));
        }

        // Criterion 5: no revive prompt left on the playback view (the revive dialog is gone;
        // the armed Revive plays back as a beat, not a question).
        [Test]
        public void CombatPlaybackView_HasNoReviveDialogMembers()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var leftovers = typeof(CombatPlaybackView).GetMembers(all)
                .Where(m => m.Name.IndexOf("ReviveDialog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("RevivePrompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("ReviveConfirm", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m => m.Name)
                .ToArray();
            Assert.IsEmpty(leftovers, "CombatPlaybackView still has revive-prompt members: " + string.Join(", ", leftovers));
        }

        // Criterion 5 (save compat): the autoConsume flag survives a round-trip in both states.
        [Test]
        public void SaveData_AutoConsumeOff_RoundTripsAsOff()
        {
            var save = new SaveData { autoConsumeRevivePotions = false };

            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.IsFalse(restored.autoConsumeRevivePotions);
        }

        // Criterion 5 (save compat): a legacy save with manualAbilityCasting set to false
        // still loads, and its other fields survive.
        [Test]
        public void SaveData_LegacyManualFlagFalse_StillLoadsOtherFields()
        {
            const string legacy = "{\"autoConsumeRevivePotions\":true,\"manualAbilityCasting\":false}";

            var save = JsonUtility.FromJson<SaveData>(legacy);

            Assert.IsNotNull(save);
            Assert.IsTrue(save.autoConsumeRevivePotions);
        }
    }
}
