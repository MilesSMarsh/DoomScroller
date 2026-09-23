using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Two sibling ways to produce a concrete, ownable weapon ItemData — both sharing the
    // same rarity->ability-count table and roll helpers, so they can never drift apart:
    //   - Generate: archetype-based (Melee/Ranged/Magic), for a generic/nameless roll —
    //     base damage + attribute-scaling shape come from a shared per-type formula.
    //   - GenerateFromDefinition: named-weapon-based — base damage + attribute-scaling
    //     come straight from a hand-authored WeaponDefinition's own per-rarity table (no
    //     formula involved), and abilities/passives roll from that weapon's own curated
    //     pool instead of the type-wide catalog.
    // Deliberately not loot *tables* — which encounter grants which rarity/weapon how
    // often is still RewardGenerator/DungeonGenerator's call; this only answers "given a
    // rarity (and optionally a specific authored weapon), what comes out."
    public static class WeaponGenerator
    {
        private const string AbilitiesResourcesPath = "Abilities";
        private const string WeaponDefinitionsResourcesPath = "WeaponDefinitions";

        // Common 0/1/2/3, Uncommon, Rare, Legendary, Mythic 4 — exactly as specified.
        // The TOTAL number of skills a weapon carries — abilities and passives combined,
        // not each independently (an Uncommon weapon gets exactly 1 skill, which might
        // be an ability or might be a passive, never one of each) — see RollSkills.
        // Capped only by however many candidates (abilities + passives together) the
        // pool being drawn from actually has, never fewer by chance. Internal (not
        // private) so WeaponUpgrader can look up how many skills a weapon should carry
        // at a given rarity (Empower/Reroll).
        internal static readonly int[] SkillCountByRarity = { 0, 1, 2, 3, 4 };

        // How much a weapon's attribute-scaling coefficient(s) grow with rarity, on top
        // of its WeaponType's Common-tier base coefficient — only used by the archetype
        // path (Generate); GenerateFromDefinition uses whatever was hand-authored per
        // rarity instead. Placeholder, tuned later like every other number introduced
        // this session.
        private static readonly float[] RarityCoefficientMultiplier = { 1.0f, 1.3f, 1.7f, 2.2f, 2.8f };

        private readonly struct AttributeShare
        {
            public readonly PlayerAttribute Attribute;
            public readonly float BaseCoefficient;

            public AttributeShare(PlayerAttribute attribute, float baseCoefficient)
            {
                Attribute = attribute;
                BaseCoefficient = baseCoefficient;
            }
        }

        private readonly struct WeaponTypeInfo
        {
            public readonly AttributeShare[] Scaling;
            public readonly bool IsTwoHanded;
            public readonly bool IsRanged;
            public readonly float AttackSpeed;

            public WeaponTypeInfo(AttributeShare[] scaling, bool isTwoHanded, bool isRanged, float attackSpeed)
            {
                Scaling = scaling;
                IsTwoHanded = isTwoHanded;
                IsRanged = isRanged;
                AttackSpeed = attackSpeed;
            }
        }

        // One representative profile per type, seeded from the 4 hand-authored weapons'
        // own numbers (Melee from Rusted Blade/Sword, Ranged from Heavy Warbow/Bow, Magic
        // from Arcane Staff/Staff) — archetype-level flavor differences (an axe swinging
        // slower than a sword) now live per-named-weapon on WeaponDefinition instead.
        private static readonly Dictionary<WeaponType, WeaponTypeInfo> TypeInfo = new()
        {
            [WeaponType.Melee] = new WeaponTypeInfo(
                new[] { new AttributeShare(PlayerAttribute.Constitution, 0.8f) }, false, false, 1.1f),
            [WeaponType.Ranged] = new WeaponTypeInfo(
                new[]
                {
                    new AttributeShare(PlayerAttribute.Agility, 0.6f),
                    new AttributeShare(PlayerAttribute.Constitution, 0.4f)
                }, true, true, 0.7f),
            [WeaponType.Magic] = new WeaponTypeInfo(
                new[]
                {
                    new AttributeShare(PlayerAttribute.Spirit, 1.0f),
                    new AttributeShare(PlayerAttribute.Constitution, 0.2f)
                }, true, true, 0.7f)
        };

        // The archetype-formula math shared by Generate and WeaponUpgrader.Empower (which
        // needs to recompute a weapon's power/scaling after raising its rarity, without
        // re-rolling anything else about it).
        internal static (int Power, AttributeScaling[] Scaling) ComputeArchetypeStats(WeaponType type, ItemRarity rarity)
        {
            var info = TypeInfo[type];
            var rarityIndex = (int)rarity;
            var power = RewardGenerator.RarityPower[rarityIndex];
            var coefficientMultiplier = RarityCoefficientMultiplier[rarityIndex];
            var scaling = info.Scaling
                .Select(share => new AttributeScaling
                {
                    attribute = share.Attribute,
                    coefficient = share.BaseCoefficient * coefficientMultiplier
                })
                .ToArray();

            return (power, scaling);
        }

        // The type-wide ability/passive pools every archetype roll (and WeaponUpgrader's
        // Empower/Reroll) draws from — every AbilityDefinition/PassiveDefinition that
        // allows this weapon type, regardless of which specific named weapon (if any)
        // this item started life as.
        internal static IEnumerable<AbilityDefinition> TypeAbilityPool(WeaponType type)
        {
            return Resources.LoadAll<AbilityDefinition>(AbilitiesResourcesPath)
                .Where(a => a.allowedWeaponTypes != null && a.allowedWeaponTypes.Contains(type));
        }

        internal static IEnumerable<PassiveDefinition> TypePassivePool(WeaponType type)
        {
            return Resources.LoadAll<PassiveDefinition>(AbilitiesResourcesPath)
                .Where(p => p.allowedWeaponTypes != null && p.allowedWeaponTypes.Contains(type));
        }

        public static ItemData Generate(ItemRarity rarity, System.Random rng = null, WeaponType? typeOverride = null)
        {
            rng ??= new System.Random();

            var types = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
            var type = typeOverride ?? types[rng.Next(types.Length)];
            var info = TypeInfo[type];
            var rarityIndex = (int)rarity;

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = $"{rarity} {type}";
            item.category = ItemCategory.Weapon;
            item.rarity = rarity;
            item.weaponType = type;
            item.isTwoHanded = info.IsTwoHanded;
            item.isRanged = info.IsRanged;
            item.attackSpeed = info.AttackSpeed;
            item.goldValue = RewardGenerator.RarityGoldValue[rarityIndex];

            var (power, scaling) = ComputeArchetypeStats(type, rarity);
            item.powerValue = power;
            item.statSummary = $"+{power} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            item.attributeScalingEntries = scaling;

            var (rolledAbilities, rolledPassives) = RollSkills(TypeAbilityPool(type), TypePassivePool(type),
                SkillCountByRarity[rarityIndex], rng);
            item.abilities = rolledAbilities;
            item.passiveEffects = rolledPassives;

            return item;
        }

        // Named-weapon path — everything numeric comes straight from the authored
        // definition's own per-rarity row, and abilities/passives roll from its own
        // curated pool rather than the type-wide catalog.
        public static ItemData GenerateFromDefinition(WeaponDefinition definition, ItemRarity rarity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var rarityIndex = (int)rarity;
            var stats = definition.statsByRarity[rarityIndex];

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = definition.weaponName;
            item.category = ItemCategory.Weapon;
            item.rarity = rarity;
            item.weaponType = definition.weaponType;
            item.damageType = definition.damageType;
            item.isTwoHanded = definition.isTwoHanded;
            item.isRanged = definition.weaponType != WeaponType.Melee;
            item.attackSpeed = definition.attackSpeed;
            item.itemColor = definition.itemColor;
            item.icon = definition.icon;
            item.goldValue = RewardGenerator.RarityGoldValue[rarityIndex];
            item.sourceDefinition = definition;

            item.powerValue = stats.baseDamage;
            item.statSummary = $"+{stats.baseDamage} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            item.attributeScalingEntries = stats.attributeScalingEntries;

            var (rolledAbilities, rolledPassives) = RollSkills(definition.possibleAbilities, definition.possiblePassives,
                SkillCountByRarity[rarityIndex], rng);
            item.abilities = rolledAbilities;
            item.passiveEffects = rolledPassives;

            return item;
        }

        // Rolls a random authored WeaponDefinition from the full catalog and generates a
        // concrete instance from it at the given rarity — for shops (Village Market, the
        // Wandering Merchant) that should sell real named weapons with curated abilities
        // and hand-authored scaling, freshly rolled each time, rather than a generic
        // archetype roll (Generate). Falls back to Generate if the catalog is empty, so
        // a shop can never come up empty just because no weapons have been authored yet.
        public static ItemData GenerateFromRandomDefinition(ItemRarity rarity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var definitions = Resources.LoadAll<WeaponDefinition>(WeaponDefinitionsResourcesPath);
            if (definitions.Length == 0)
            {
                return Generate(rarity, rng);
            }

            var definition = definitions[rng.Next(definitions.Length)];
            return GenerateFromDefinition(definition, rarity, rng);
        }

        // Internal (not private) so WeaponUpgrader.Reroll can draw a fresh ability set
        // the same way an initial roll does.
        internal static WeaponAbilityData[] RollAbilities(IEnumerable<AbilityDefinition> pool, int desiredCount, System.Random rng)
        {
            if (desiredCount <= 0 || pool == null)
            {
                return System.Array.Empty<WeaponAbilityData>();
            }

            var shuffled = pool.Where(a => a != null).ToList();
            Shuffle(shuffled, rng);
            return shuffled.Take(desiredCount).Select(a => a.data).ToArray();
        }

        // Same shuffle-and-take shape as RollAbilities (and the same sharing convention:
        // this is internal, not private, so WeaponUpgrader.Reroll/Empower can draw a
        // fresh/extra passive the same way an initial roll does) — a weapon always
        // carries exactly min(desiredCount, pool size) passives, never fewer by chance.
        internal static StatusEffectData[] RollPassives(IEnumerable<PassiveDefinition> pool, int desiredCount, System.Random rng)
        {
            if (desiredCount <= 0 || pool == null)
            {
                return System.Array.Empty<StatusEffectData>();
            }

            var shuffled = pool.Where(p => p != null).ToList();
            Shuffle(shuffled, rng);
            return shuffled.Take(desiredCount).Select(p => p.effect).ToArray();
        }

        // One slot in a combined ability/passive draw — see RollSkills.
        private readonly struct SkillCandidate
        {
            public readonly AbilityDefinition Ability;
            public readonly PassiveDefinition Passive;
            public bool IsAbility => Ability != null;

            public SkillCandidate(AbilityDefinition ability)
            {
                Ability = ability;
                Passive = null;
            }

            public SkillCandidate(PassiveDefinition passive)
            {
                Ability = null;
                Passive = passive;
            }
        }

        // A weapon's skill count (SkillCountByRarity) is a total across abilities AND
        // passives together, not each independently — an Uncommon weapon gets exactly 1
        // skill, which could land as either. Shuffles both pools together into one list
        // so every candidate (ability or passive) has an equal chance per slot,
        // proportional to however many of each the pool actually holds, then splits the
        // chosen slots back into the two output arrays. Internal (not private) so
        // WeaponUpgrader.Reroll can draw a fresh combined set the same way an initial
        // roll does.
        internal static (WeaponAbilityData[] Abilities, StatusEffectData[] Passives) RollSkills(
            IEnumerable<AbilityDefinition> abilityPool, IEnumerable<PassiveDefinition> passivePool, int desiredCount,
            System.Random rng)
        {
            if (desiredCount <= 0)
            {
                return (System.Array.Empty<WeaponAbilityData>(), System.Array.Empty<StatusEffectData>());
            }

            var candidates = new List<SkillCandidate>();
            if (abilityPool != null)
            {
                candidates.AddRange(abilityPool.Where(a => a != null).Select(a => new SkillCandidate(a)));
            }

            if (passivePool != null)
            {
                candidates.AddRange(passivePool.Where(p => p != null).Select(p => new SkillCandidate(p)));
            }

            Shuffle(candidates, rng);
            var chosen = candidates.Take(desiredCount).ToList();

            var abilities = chosen.Where(c => c.IsAbility).Select(c => c.Ability.data).ToArray();
            var passives = chosen.Where(c => !c.IsAbility).Select(c => c.Passive.effect).ToArray();
            return (abilities, passives);
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
