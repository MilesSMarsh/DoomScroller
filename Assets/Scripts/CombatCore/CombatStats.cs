using System;

namespace DoomScroller.CombatCore
{
    // The stat math shared by the fight itself (CombatSimulator) and the feed badge's
    // difficulty estimate, so the two can never drift apart.
    public static class CombatStats
    {
        public const int UnarmedAttackDamage = 3;
        public const float UnarmedAttackSpeed = 0.6f;
        public const float MinIntervalSeconds = 0.05f;

        // Global attribute effects that aren't weapon-specific (Constitution's/Agility's/
        // Spirit's damage halves are expressed purely through each weapon's own scaling
        // entries instead).
        public const float AgilityAttackSpeedPerPoint = 0.015f;
        public const float SpiritCooldownDivisorPerPoint = 0.03f;

        // Banker's rounding, matching the engine's own RoundToInt so results stay identical.
        public static int RoundToInt(float value)
        {
            return (int)Math.Round(value, MidpointRounding.ToEven);
        }

        public static int Attribute(PlayerSideInput player, int index)
        {
            var values = player.attributes;
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        public readonly struct WeaponAttackProfile
        {
            public readonly int Damage;
            public readonly float Speed;
            public readonly bool IsRanged;

            public WeaponAttackProfile(int damage, float speed, bool isRanged)
            {
                Damage = damage;
                Speed = speed;
                IsRanged = isRanged;
            }
        }

        public readonly struct AbilityProfile
        {
            public readonly string Name;
            public readonly int Damage;
            public readonly float Cooldown;

            public AbilityProfile(string name, int damage, float cooldown)
            {
                Name = name;
                Damage = damage;
                Cooldown = cooldown;
            }
        }

        // Every equipped item's passive effects folded into scalar totals.
        public readonly struct PassiveTotals
        {
            public readonly float SelfDamageMultiplier;
            public readonly float SelfSpeedMultiplier;
            public readonly int SelfArmor;
            public readonly float EnemyDamageMultiplier;
            public readonly float EnemySpeedMultiplier;
            public readonly float EnemyDotDps;

            public PassiveTotals(float selfDamageMultiplier, float selfSpeedMultiplier, int selfArmor,
                float enemyDamageMultiplier, float enemySpeedMultiplier, float enemyDotDps)
            {
                SelfDamageMultiplier = selfDamageMultiplier;
                SelfSpeedMultiplier = selfSpeedMultiplier;
                SelfArmor = selfArmor;
                EnemyDamageMultiplier = enemyDamageMultiplier;
                EnemySpeedMultiplier = enemySpeedMultiplier;
                EnemyDotDps = enemyDotDps;
            }
        }

        // 1x when the enemy has no multiplier for this damage type.
        public static float DamageTypeMultiplier(int damageType, DamageMultiplierInput[] multipliers)
        {
            if (multipliers == null)
            {
                return 1f;
            }

            foreach (var modifier in multipliers)
            {
                if (modifier.damageType == damageType)
                {
                    return modifier.multiplier;
                }
            }

            return 1f;
        }

        public static int ScalingBonus(PlayerSideInput player, WeaponInput weapon)
        {
            if (weapon.scaling == null || weapon.scaling.Length == 0)
            {
                return 0;
            }

            var bonus = 0f;
            foreach (var entry in weapon.scaling)
            {
                bonus += Attribute(player, entry.attribute) * entry.coefficient;
            }

            return RoundToInt(bonus);
        }

        public static PassiveTotals ComputePassiveTotals(PlayerSideInput player)
        {
            var selfDamageBonus = 0f;
            var selfSpeedBonus = 0f;
            var selfArmor = 0f;
            var enemyDamageReduction = 0f;
            var enemySpeedPenalty = 0f;
            var enemyDotDps = 0f;

            if (player.passives != null)
            {
                foreach (var effect in player.passives)
                {
                    var magnitude = effect.scalesWithAttribute
                        ? effect.magnitude + Attribute(player, effect.scalingAttribute) * effect.scalingCoefficient
                        : effect.magnitude;
                    switch (effect.effectType)
                    {
                        case StatusEffectType.EmpowerDamage:
                            selfDamageBonus += magnitude;
                            break;
                        case StatusEffectType.HasteAttackSpeed:
                            selfSpeedBonus += magnitude;
                            break;
                        case StatusEffectType.ArmorUp:
                            selfArmor += magnitude;
                            break;
                        case StatusEffectType.WeakenDamage:
                            enemyDamageReduction += magnitude;
                            break;
                        case StatusEffectType.SlowAttackSpeed:
                            enemySpeedPenalty += magnitude;
                            break;
                        case StatusEffectType.DamageOverTime:
                            enemyDotDps += magnitude / Math.Max(MinIntervalSeconds, effect.tickIntervalSeconds);
                            break;
                    }
                }
            }

            return new PassiveTotals(1f + selfDamageBonus, 1f + selfSpeedBonus, RoundToInt(selfArmor),
                Math.Max(0f, 1f - enemyDamageReduction), 1f + enemySpeedPenalty, enemyDotDps);
        }

        public static WeaponAttackProfile ComputeBasicAttackProfile(PlayerSideInput player, PassiveTotals passives,
            DamageMultiplierInput[] enemyMultipliers)
        {
            var weapons = player.weapons ?? new WeaponInput[0];

            // Each weapon's own damage type is checked against the enemy's multipliers
            // independently, then summed — a dual-wielder mixing damage types shouldn't
            // have one shared multiplier applied to the total.
            float rawDamage;
            float baseSpeed;
            var isRanged = false;
            if (weapons.Length > 0)
            {
                rawDamage = 0f;
                var speedSum = 0f;
                foreach (var w in weapons)
                {
                    rawDamage += (w.power + ScalingBonus(player, w)) * DamageTypeMultiplier(w.damageType, enemyMultipliers);
                    speedSum += w.attackSpeed;
                    isRanged |= w.isRanged;
                }

                baseSpeed = speedSum / weapons.Length;
            }
            else
            {
                rawDamage = UnarmedAttackDamage;
                baseSpeed = UnarmedAttackSpeed;
            }

            var damage = RoundToInt(rawDamage * passives.SelfDamageMultiplier);
            var speed = baseSpeed + Attribute(player, AttributeIndex.Agility) * AgilityAttackSpeedPerPoint;
            speed *= passives.SelfSpeedMultiplier;

            return new WeaponAttackProfile(damage, speed, isRanged);
        }

        public static AbilityProfile[] ComputeAbilityProfiles(PlayerSideInput player, PassiveTotals passives,
            DamageMultiplierInput[] enemyMultipliers)
        {
            var abilities = player.abilities ?? new AbilityInput[0];
            var result = new AbilityProfile[abilities.Length];
            var cooldownDivisor = 1f + Attribute(player, AttributeIndex.Spirit) * SpiritCooldownDivisorPerPoint;
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                var weapon = player.weapons[ability.weaponIndex];

                // An ability hits harder than a basic swing while scaling identically
                // off whatever the weapon scales off, plus the ability's own bonus.
                var multiplier = DamageTypeMultiplier(weapon.damageType, enemyMultipliers);
                var damage = RoundToInt(
                    (weapon.power + ScalingBonus(player, weapon) + ability.powerBonus) * multiplier * passives.SelfDamageMultiplier);
                var cooldown = Math.Max(MinIntervalSeconds, ability.cooldownSeconds / cooldownDivisor);
                result[i] = new AbilityProfile(ability.name, damage, cooldown);
            }

            return result;
        }

        // Steady-state DPS with variance/misses ignored ("perfect rng") — used by the
        // feed badge's difficulty heuristic, not by the fight itself.
        public static float EstimatePlayerDps(PlayerSideInput player, DamageMultiplierInput[] enemyMultipliers)
        {
            var passives = ComputePassiveTotals(player);
            var basicAttack = ComputeBasicAttackProfile(player, passives, enemyMultipliers);
            var dps = basicAttack.Damage * basicAttack.Speed;

            foreach (var ability in ComputeAbilityProfiles(player, passives, enemyMultipliers))
            {
                dps += ability.Damage / ability.Cooldown;
            }

            dps += passives.EnemyDotDps;
            return dps;
        }

        public static int EstimatePlayerDefense(PlayerSideInput player)
        {
            return player.defense + ComputePassiveTotals(player).SelfArmor;
        }
    }
}
