using System;
using System.Collections.Generic;
using DoomScroller.CombatCore;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;

namespace DoomScroller.Combat
{
    // The adapter between the game's Unity-side data (loadout, attributes, encounters)
    // and CombatCore's plain-data FightInput. Everything that reads an authored asset
    // lives here so CombatCore never has to.
    public static class FightInputBuilder
    {
        // A resource node's timed damage window — the "enemy" never attacks and can't
        // meaningfully die (an arbitrarily large health pool the timer will never drain).
        public const int ResourceNodeEffectiveHealth = 999_999_999;

        private static readonly EquipmentSlotType[] HandSlots =
        {
            EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand
        };

        // A plain single-resolution encounter (Enemy feed card): enemy stats straight
        // from the authored asset.
        public static FightInput ForEncounter(PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings,
            EncounterData data, int playerStartHp, int seed)
        {
            return Build(loadout, attributes, settings, playerStartHp, seed, data.enemyHealth, data.enemyAttackDamage,
                data.enemyAttackSpeed, data.damageTypeModifiers, canAttack: true);
        }

        // One page of a dungeon run: enemy stats from the room the player authored.
        public static FightInput ForStep(PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings,
            DungeonStep step, int playerStartHp, int seed)
        {
            return Build(loadout, attributes, settings, playerStartHp, seed, step.EnemyHealth, step.EnemyAttackDamage,
                step.EnemyAttackSpeed, step.EnemyWeaknesses, canAttack: true);
        }

        // A resource node's timed window: no enemy timer, no revive.
        public static FightInput ForResource(PlayerLoadout loadout, PlayerAttributes attributes, EncounterData data,
            int playerStartHp, int seed)
        {
            return Build(loadout, attributes, null, playerStartHp, seed, ResourceNodeEffectiveHealth, 0, 1f,
                data.resourceWeaknesses, canAttack: false);
        }

        public static FightInput Build(PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings,
            int playerStartHp, int seed, int enemyHealth, int enemyAttackDamage, float enemyAttackSpeed,
            DamageTypeModifier[] enemyWeaknesses, bool canAttack)
        {
            var input = new FightInput
            {
                player = BuildPlayer(loadout, attributes, playerStartHp),
                enemy = new EnemySideInput
                {
                    maxHp = enemyHealth,
                    attackDamage = enemyAttackDamage,
                    attackSpeed = enemyAttackSpeed,
                    canAttack = canAttack,
                    damageMultipliers = ToMultipliers(enemyWeaknesses)
                },
                seed = seed
            };

            // Until the per-card toggle exists, "armed" means a Revive potion is carried
            // and the player has auto-consume on. It fires inside the simulation.
            if (settings != null && settings.AutoConsumeRevivePotions)
            {
                var potion = loadout.GetBestRevivePotion();
                if (potion != null)
                {
                    input.reviveArmed = true;
                    input.revivePercent = loadout.GetRevivePercent(potion);
                }
            }

            return input;
        }

        public static PlayerSideInput BuildPlayer(PlayerLoadout loadout, PlayerAttributes attributes, int startHp)
        {
            var player = new PlayerSideInput { startHp = startHp };

            var values = new int[Enum.GetValues(typeof(PlayerAttribute)).Length];
            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                values[(int)attribute] = attributes.Get(attribute);
            }

            player.attributes = values;

            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null && item.category != ItemCategory.Weapon)
                {
                    player.defense += item.powerValue;
                }
            }

            var weaponItems = new List<ItemData>();
            foreach (var slot in HandSlots)
            {
                var item = loadout.GetEquipped(slot);
                if (item?.category == ItemCategory.Weapon)
                {
                    weaponItems.Add(item);
                }
            }

            var weapons = new WeaponInput[weaponItems.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = ToWeapon(weaponItems[i]);
            }

            player.weapons = weapons;

            var abilities = new List<AbilityInput>();
            foreach (var (weapon, ability) in loadout.GetEquippedWeaponAbilityPairs())
            {
                abilities.Add(new AbilityInput
                {
                    name = ability.abilityName,
                    weaponIndex = weaponItems.IndexOf(weapon),
                    powerBonus = ability.powerBonus,
                    cooldownSeconds = ability.cooldownSeconds,
                    onCastEffects = ToEffects(ability.onCastEffects)
                });
            }

            player.abilities = abilities.ToArray();

            var passives = new List<PassiveInput>();
            foreach (var effect in loadout.GetEquippedPassiveEffects())
            {
                passives.Add(new PassiveInput
                {
                    effectType = effect.effectType,
                    magnitude = effect.magnitude,
                    tickIntervalSeconds = effect.tickIntervalSeconds,
                    scalesWithAttribute = effect.scalesWithAttribute,
                    scalingAttribute = (int)effect.scalingAttribute,
                    scalingCoefficient = effect.scalingCoefficient
                });
            }

            player.passives = passives.ToArray();
            return player;
        }

        private static WeaponInput ToWeapon(ItemData item)
        {
            var scaling = new List<ScalingInput>();
            if (item.attributeScalingEntries != null)
            {
                foreach (var entry in item.attributeScalingEntries)
                {
                    scaling.Add(new ScalingInput { attribute = (int)entry.attribute, coefficient = entry.coefficient });
                }
            }

            return new WeaponInput
            {
                power = item.powerValue,
                attackSpeed = item.attackSpeed,
                isRanged = item.isRanged,
                damageType = (int)item.damageType,
                scaling = scaling.ToArray()
            };
        }

        private static EffectInput[] ToEffects(StatusEffectData[] effects)
        {
            if (effects == null)
            {
                return new EffectInput[0];
            }

            var result = new EffectInput[effects.Length];
            for (var i = 0; i < effects.Length; i++)
            {
                result[i] = new EffectInput
                {
                    effectType = effects[i].effectType,
                    magnitude = effects[i].magnitude,
                    durationSeconds = effects[i].durationSeconds,
                    tickIntervalSeconds = effects[i].tickIntervalSeconds
                };
            }

            return result;
        }

        private static DamageMultiplierInput[] ToMultipliers(DamageTypeModifier[] modifiers)
        {
            if (modifiers == null)
            {
                return new DamageMultiplierInput[0];
            }

            var result = new DamageMultiplierInput[modifiers.Length];
            for (var i = 0; i < modifiers.Length; i++)
            {
                result[i] = new DamageMultiplierInput
                {
                    damageType = (int)modifiers[i].type,
                    multiplier = modifiers[i].multiplier
                };
            }

            return result;
        }

        // Feed-badge difficulty estimates — the same CombatStats math a real fight seeds
        // its timers from, so the badge can't drift from the fight.
        public static float EstimatePlayerDps(PlayerLoadout loadout, PlayerAttributes attributes,
            DamageTypeModifier[] enemyWeaknesses = null)
        {
            return CombatStats.EstimatePlayerDps(BuildPlayer(loadout, attributes, 1), ToMultipliers(enemyWeaknesses));
        }

        public static int EstimatePlayerDefense(PlayerLoadout loadout, PlayerAttributes attributes)
        {
            return CombatStats.EstimatePlayerDefense(BuildPlayer(loadout, attributes, 1));
        }

        public static float EstimateEnemyDamageMultiplier(PlayerLoadout loadout, PlayerAttributes attributes)
        {
            return CombatStats.ComputePassiveTotals(BuildPlayer(loadout, attributes, 1)).EnemyDamageMultiplier;
        }

        public static float EstimateEnemySpeedMultiplier(PlayerLoadout loadout, PlayerAttributes attributes)
        {
            return CombatStats.ComputePassiveTotals(BuildPlayer(loadout, attributes, 1)).EnemySpeedMultiplier;
        }
    }
}
