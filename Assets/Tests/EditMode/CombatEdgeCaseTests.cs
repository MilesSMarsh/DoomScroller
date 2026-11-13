using System.Linq;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class CombatEdgeCaseTests
    {
        [Test]
        public void MaxBeats_NeitherSideCanDieNaturally_ResolvesToTheHealthierFractionSide()
        {
            // Both pools are far too large for ~200 floor-of-1 hits to drain either one
            // naturally, so the 200-beat safety cap is guaranteed to be what ends this
            // fight, not a real death.
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 100000, 100000,
                enemyAttackDamage: 1, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            var beats = sim.RunUntilDeathOrVictory();

            Assert.AreEqual(200, beats.Count);
            Assert.IsTrue(sim.IsPlayerDead || sim.IsEnemyDead, "the safety cap must force a decision");
            Assert.IsFalse(sim.IsPlayerDead && sim.IsEnemyDead, "never both — the tie-break only zeroes one side");
        }

        [Test]
        public void MaxBeats_TieBreak_FavorsWhicheverSideKeptTheHealthierFraction()
        {
            // Enemy pool is small enough that ~100 player hits (of 1 each) leave it at a
            // low remaining fraction, while the player's own huge pool barely moves —
            // the tie-break should hand the enemy the loss.
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 100000, 500,
                enemyAttackDamage: 0, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            sim.RunUntilDeathOrVictory();

            Assert.IsTrue(sim.IsEnemyDead);
            Assert.IsFalse(sim.IsPlayerDead);
            Assert.AreEqual(0, sim.EnemyHp);
        }

        [Test]
        public void ArmedRevive_FiresOnce_AndTheSameFightCarriesOn()
        {
            // Enemy hits hard enough to guarantee the player dies first; the enemy's
            // huge pool guarantees it doesn't also die in that same first stretch.
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var armed = TestFixtures.MakeSim(loadout, new PlayerAttributes(), playerStartHp: 20,
                enemyMaxHealth: 100000, enemyAttackDamage: 50, enemyAttackSpeed: 1f, enemyWeaknesses: null,
                rng: new FixedRandom(), reviveArmedPercent: 100);
            var beats = armed.RunUntilDeathOrVictory();

            Assert.IsTrue(armed.ReviveFired);
            Assert.AreEqual(20, armed.ReviveHp, "100% of a 20 HP start");
            Assert.That(armed.ReviveBeatIndex, Is.InRange(0, beats.Count - 2), "the fight continued past the revive");
            Assert.AreEqual(CombatActor.Enemy, beats[armed.ReviveBeatIndex].Actor, "the enemy's hit killed the player");

            var unarmed = TestFixtures.MakeSim(loadout, new PlayerAttributes(), playerStartHp: 20,
                enemyMaxHealth: 100000, enemyAttackDamage: 50, enemyAttackSpeed: 1f, enemyWeaknesses: null,
                rng: new FixedRandom());
            var unarmedBeats = unarmed.RunUntilDeathOrVictory();
            Assert.IsFalse(unarmed.ReviveFired);
            Assert.Greater(beats.Count, unarmedBeats.Count, "the revived fight runs longer");
            Assert.LessOrEqual(armed.EnemyHp, unarmed.EnemyHp, "damage already dealt to the enemy must carry over");
        }

        [Test]
        public void EnemyCannotAttack_EveryBeatIsThePlayers_AndPlayerHpNeverMoves()
        {
            // The resource-node minigame's construction — enemyAttackDamage/Speed are
            // still passed but must have zero effect once enemyCanAttack is false.
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 2f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), playerStartHp: 100,
                enemyMaxHealth: 999_999_999, enemyAttackDamage: 500, enemyAttackSpeed: 20f, enemyWeaknesses: null,
                rng: new FixedRandom(), enemyCanAttack: false);

            var beats = sim.RunForDuration(5f);

            Assert.IsNotEmpty(beats);
            Assert.IsTrue(beats.All(b => b.Actor == CombatActor.Player), "the node never gets a turn");
            Assert.AreEqual(100, sim.PlayerHp, "nothing can ever damage the player in this mode");
            Assert.Greater(sim.DamageDealt, 0);
            Assert.AreEqual(sim.EnemyMaxHp - sim.EnemyHp, sim.DamageDealt);
        }

        [Test]
        public void RunForDuration_StopsAtTheTimeLimit_NotOnDeathOrMaxBeats()
        {
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), playerStartHp: 100,
                enemyMaxHealth: 999_999_999, enemyAttackDamage: 0, enemyAttackSpeed: 1f, enemyWeaknesses: null,
                rng: new FixedRandom(), enemyCanAttack: false);

            // Default PlayerAttributes seed Agility at BaseValue (5), adding
            // 5*0.015=0.075 to the weapon's own 1/s, so beats land at intervals of
            // 1/1.075 ≈ 0.9302s: t≈0, 0.930, 1.860, 2.791, next≈3.721 (excluded) — 4
            // beats in a 3.5s window, not however many MaxBeats(200) would allow.
            var beats = sim.RunForDuration(3.5f);

            Assert.AreEqual(4, beats.Count);
        }
    }
}
