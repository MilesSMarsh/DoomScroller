using System.Linq;
using DoomScroller.Combat;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Integration coverage for the user's actual authored content (Stone Sword + its
    // two abilities, Slash and Lacerate, and its two wired passives, EmpowerDamage and
    // WeakenDamage) rather than just the generic mechanism — regression coverage for
    // the bug report: rolling this weapon never gave more than 1 passive, and ability/
    // passive counts weren't deterministic at a rarity the pool could fully satisfy.
    public class StoneSwordIntegrationTests
    {
        // Mirrors Assets/Resources/Abilities/Ability_Slash.asset exactly.
        private static WeaponAbilityData MakeSlash() => new WeaponAbilityData
        {
            abilityName = "Slash", powerBonus = 5, cooldownSeconds = 3f
        };

        // Mirrors Assets/Resources/Abilities/Ability_Lacerate.asset exactly.
        private static WeaponAbilityData MakeLacerate() => new WeaponAbilityData
        {
            abilityName = "Lacerate", powerBonus = 5, cooldownSeconds = 5f,
            onCastEffects = new[]
            {
                new StatusEffectData { effectType = StatusEffectType.DamageOverTime, magnitude = 1f, durationSeconds = 3f, tickIntervalSeconds = 1f }
            }
        };

        // Mirrors Assets/Resources/Abilities/Passive_DamageUp.asset exactly.
        private static StatusEffectData MakeEmpowerPassive() =>
            new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f };

        // Mirrors Assets/Resources/Abilities/Passive_WeakenEnemy.asset exactly.
        private static StatusEffectData MakeWeakenPassive() =>
            new StatusEffectData { effectType = StatusEffectType.WeakenDamage, magnitude = 0.25f };

        // Mirrors Assets/Resources/WeaponDefinitions/StoneSword.asset's Runed-tier row
        // (baseDamage 4, Constitution x0.5) — the lowest rarity at which the pool of 2
        // abilities/2 passives is fully satisfied (SkillCountByRarity[Runed] == 2).
        private static ItemData MakeStoneSwordAtRare(WeaponAbilityData[] abilities = null, StatusEffectData[] passives = null)
        {
            return TestFixtures.MakeWeapon(power: 4, attackSpeed: 1f, damageType: DamageType.Slash,
                scaling: new[] { new AttributeScaling { attribute = PlayerAttribute.Constitution, coefficient = 0.5f } },
                abilities: abilities, passives: passives);
        }

        [Test]
        public void RealAsset_GenerateFromDefinition_AtMythicRarity_GrantsExactlyFourTotalSkills()
        {
            // Exalted allows 4 TOTAL skills — abilities and passives combined, not each
            // independently — capped at however many candidates Stone Sword's own pools
            // hold altogether (currently 2 abilities + 3 passives = 5, so this gets 4 of
            // those 5, split unpredictably between the two kinds).
            var definition = Resources.Load<WeaponDefinition>("WeaponDefinitions/HallowWoodSword");
            Assert.IsNotNull(definition, "expected Assets/Resources/WeaponDefinitions/HallowWoodSword.asset to exist");

            var item = WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Exalted, new System.Random(1));

            var combinedPoolSize = definition.possibleAbilities.Length + definition.possiblePassives.Length;
            var expectedTotal = System.Math.Min(4, combinedPoolSize);
            Assert.AreEqual(expectedTotal, item.abilities.Length + item.passiveEffects.Length);

            var abilityNames = definition.possibleAbilities.Select(a => a.data.abilityName).ToList();
            var passiveTypes = definition.possiblePassives.Select(p => p.effect.effectType).ToList();
            Assert.IsTrue(item.abilities.All(a => abilityNames.Contains(a.abilityName)), "every rolled ability must come from this weapon's own pool");
            Assert.IsTrue(item.passiveEffects.All(p => passiveTypes.Contains(p.effectType)), "every rolled passive must come from this weapon's own pool");

            Assert.AreSame(definition, item.sourceDefinition,
                "WeaponUpgrader.Empower/Reroll need this to restrict rolls to this weapon's own pool");
        }

        [Test]
        public void RealAsset_GenerateFromDefinition_AtUncommonRarity_NeverGrantsBothAnAbilityAndAPassive()
        {
            // The user's own bug report against their real content: Tempered
            // (SkillCountByRarity == 1) used to give 1 ability AND 1 passive.
            var definition = Resources.Load<WeaponDefinition>("WeaponDefinitions/HallowWoodSword");

            for (var seed = 0; seed < 30; seed++)
            {
                var item = WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Tempered, new System.Random(seed));

                Assert.AreEqual(1, item.abilities.Length + item.passiveEffects.Length);
                Assert.IsFalse(item.abilities.Length == 1 && item.passiveEffects.Length == 1);
            }
        }

        [Test]
        public void ItemSnapshot_RoundTrips_SourceDefinition_AcrossSaveAndLoad()
        {
            var definition = Resources.Load<WeaponDefinition>("WeaponDefinitions/HallowWoodSword");
            var item = WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Runed, new System.Random(1));
            Assert.IsNotNull(item.sourceDefinition);

            var snapshot = ItemSnapshot.Capture(item);
            var restored = snapshot.Restore();

            Assert.IsNotNull(restored.sourceDefinition,
                "a saved+reloaded weapon must not silently lose the ability to Empower/Reroll from its own pool");
            Assert.AreSame(definition, restored.sourceDefinition, "should resolve back to the same live catalog asset");
        }

        [Test]
        public void Slash_Alone_DealsExactDamage_OnItsOwnCooldown_NoSideEffects()
        {
            var weapon = MakeStoneSwordAtRare(abilities: new[] { MakeSlash() });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000, 100000, 0, 0.0001f, null,
                new FixedRandom());
            sim.AdvanceManualCooldowns(9999f);

            var beat = sim.CastManualAbility("Slash");
            Assert.IsNotNull(beat);
            // baseDamage(4) + round(Constitution(5) * 0.5 = 2) + powerBonus(5) = 11.
            Assert.AreEqual(11, beat.Value.DisplayDamage);
            Assert.IsNull(sim.CastManualAbility("Slash"), "should be back on cooldown immediately");
        }

        [Test]
        public void Lacerate_Alone_DealsExactDamage_AndItsBleedTicksIndependently()
        {
            var weapon = MakeStoneSwordAtRare(abilities: new[] { MakeLacerate() });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000000, 1000000, 0,
                0.0000001f, null, new FixedRandom());
            sim.AdvanceManualCooldowns(9999f);

            var beat = sim.CastManualAbility("Lacerate");
            Assert.IsNotNull(beat);
            Assert.AreEqual(11, beat.Value.DisplayDamage);

            // The bleed it applies on cast ticks on its own schedule, independent of the
            // hit that triggered it (magnitude 1, every 1s, for 3s -> 3 ticks).
            var ticks = sim.RunUntilDeathOrVictory().Where(b => b.IsDotTick).ToList();
            Assert.AreEqual(3, ticks.Count);
            Assert.IsTrue(ticks.All(t => t.DisplayDamage == 1));
        }

        [Test]
        public void EmpowerPassive_Alone_BoostsBasicAttackDamage_ByExactlyTenPercent()
        {
            var weapon = MakeStoneSwordAtRare(passives: new[] { MakeEmpowerPassive() });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), new GameSettings(), 1000, 100000, 0, 1f,
                null, new FixedRandom());
            var beat = sim.RunUntilDeathOrVictory().First(b => b.Actor == CombatActor.Player);

            // (4 + 2) * 1.1 = 6.6 -> 7.
            Assert.AreEqual(7, beat.DisplayDamage);
        }

        [Test]
        public void WeakenPassive_Alone_ReducesEnemyDamage_ByExactlyTwentyFivePercent()
        {
            var weapon = MakeStoneSwordAtRare(passives: new[] { MakeWeakenPassive() });
            var loadout = TestFixtures.MakeLoadout(weapon);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), new GameSettings(), 1000, 100000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            var beat = sim.RunUntilDeathOrVictory().First(b => b.Actor == CombatActor.Enemy);

            // 20 * (1 - 0.25) = 15, no armor equipped to reduce it further.
            Assert.AreEqual(15, beat.DisplayDamage);
        }

        [Test]
        public void AllFourSkills_Together_EachStillWorksIndependently_NoCrossInterference()
        {
            var weapon = MakeStoneSwordAtRare(
                abilities: new[] { MakeSlash(), MakeLacerate() },
                passives: new[] { MakeEmpowerPassive(), MakeWeakenPassive() });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var settings = new GameSettings();
            settings.SetManualAbilityCasting(true);

            var sim = new CombatSimulator(loadout, new PlayerAttributes(), settings, 1000000, 1000000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            sim.AdvanceManualCooldowns(9999f);

            // Both abilities are independently tracked/castable — casting one doesn't
            // consume or interfere with the other's cooldown or damage. Done before the
            // RunUntilDeathOrVictory call below: with both HP pools this large the fight
            // never resolves naturally within its 200-beat budget, and once that budget
            // is spent the MaxBeats safety-cap forces one side to 0 HP — which would
            // make every CastManualAbility call afterward refuse outright.
            var slashBeat = sim.CastManualAbility("Slash");
            Assert.IsNotNull(slashBeat);
            Assert.AreEqual(12, slashBeat.Value.DisplayDamage, "(4+2+5)*1.1 = 12.1 -> 12, Empower layered on top");

            var lacerateBeat = sim.CastManualAbility("Lacerate");
            Assert.IsNotNull(lacerateBeat, "Lacerate's own cooldown is untouched by Slash having just been cast");
            Assert.AreEqual(12, lacerateBeat.Value.DisplayDamage);

            Assert.IsNull(sim.CastManualAbility("Slash"), "Slash is on its own cooldown again");
            Assert.IsNull(sim.CastManualAbility("Lacerate"), "Lacerate is on its own cooldown again");

            var beats = sim.RunUntilDeathOrVictory();

            // Empower is a passive (always active, no cast needed) — the basic attack
            // already reflects it even with everything else equipped.
            var basicAttackBeat = beats.First(b => b.Actor == CombatActor.Player && b.AbilityName == null);
            Assert.AreEqual(7, basicAttackBeat.DisplayDamage, "Empower still applies with everything else equipped");

            // Weaken is likewise always active — the enemy's hit already reflects it.
            var enemyBeat = beats.First(b => b.Actor == CombatActor.Enemy);
            Assert.AreEqual(15, enemyBeat.DisplayDamage, "Weaken still applies with everything else equipped");
        }
    }
}
