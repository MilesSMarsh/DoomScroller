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

        // Common 0/1/2/3, Uncommon, Rare, Legendary, Mythic 4 — exactly as specified.
        // Capped at however many matching abilities actually exist in the pool being
        // drawn from (a thin pool just yields fewer abilities, not an error). Internal
        // (not private) so WeaponUpgrader can look up how many abilities a weapon should
        // carry at a given rarity (Empower/Reroll).
        internal static readonly int[] AbilityCountByRarity = { 0, 1, 2, 3, 4 };

        // How much a weapon's attribute-scaling coefficient(s) grow with rarity, on top
        // of its WeaponType's Common-tier base coefficient — only used by the archetype
        // path (Generate); GenerateFromDefinition uses whatever was hand-authored per
        // rarity instead. Placeholder, tuned later like every other number introduced
        // this session.
        private static readonly float[] RarityCoefficientMultiplier = { 1.0f, 1.3f, 1.7f, 2.2f, 2.8f };

        // Shared with EquipmentGenerator (internal, not private) — armor/talismans roll
        // a passive the same way weapons do.
        internal const float PassiveRollChance = 0.3f;

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
            item.slotType = RollHandSlot(rng);
            item.isTwoHanded = info.IsTwoHanded;
            item.isRanged = info.IsRanged;
            item.attackSpeed = info.AttackSpeed;
            item.goldValue = RewardGenerator.RarityGoldValue[rarityIndex];

            var (power, scaling) = ComputeArchetypeStats(type, rarity);
            item.powerValue = power;
            item.statSummary = $"+{power} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            item.attributeScalingEntries = scaling;

            item.abilities = RollAbilities(TypeAbilityPool(type), AbilityCountByRarity[rarityIndex], rng);
            item.passiveEffects = RollPassive(TypePassivePool(type), rng);

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
            item.slotType = RollHandSlot(rng);
            item.isTwoHanded = definition.isTwoHanded;
            item.isRanged = definition.weaponType != WeaponType.Melee;
            item.attackSpeed = definition.attackSpeed;
            item.itemColor = definition.itemColor;
            item.icon = definition.icon;
            item.goldValue = RewardGenerator.RarityGoldValue[rarityIndex];

            item.powerValue = stats.baseDamage;
            item.statSummary = $"+{stats.baseDamage} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            item.attributeScalingEntries = stats.attributeScalingEntries;

            item.abilities = RollAbilities(definition.possibleAbilities, AbilityCountByRarity[rarityIndex], rng);
            item.passiveEffects = RollPassive(definition.possiblePassives, rng);

            return item;
        }

        // Randomized like RewardGenerator's own slot roll — a two-handed weapon clears
        // the opposite hand regardless of which one it's nominally assigned to (see
        // PlayerLoadout.Equip), but a fixed hand would mean it never shows up browsing
        // the *other* hand slot's options in EquipSlotDetailView.
        private static EquipmentSlotType RollHandSlot(System.Random rng)
        {
            return rng.Next(2) == 0 ? EquipmentSlotType.LeftHand : EquipmentSlotType.RightHand;
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

        internal static StatusEffectData[] RollPassive(IEnumerable<PassiveDefinition> pool, System.Random rng)
        {
            if (pool == null || rng.NextDouble() >= PassiveRollChance)
            {
                return null;
            }

            var candidates = pool.Where(p => p != null).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            return new[] { candidates[rng.Next(candidates.Count)].effect };
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
