using System;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // One rarity tier's authored numbers for a WeaponDefinition — index i in
    // statsByRarity below corresponds to ItemRarity (i) (Common/Uncommon/Rare/Legendary).
    [Serializable]
    public class WeaponRarityStats
    {
        public int baseDamage;
        public AttributeScaling[] attributeScalingEntries;
    }

    // A hand-authored weapon *concept* — distinct from ItemData, which represents one
    // concrete, ownable instance at one rarity. This is the "list of every weapon, its
    // base damage at each rarity, and its allowed abilities" the Weapon Catalog window
    // (Assets/Editor/WeaponCatalogWindow.cs) lists and edits. Turning one of these into
    // an actual ItemData at a specific rarity is WeaponGenerator.GenerateFromDefinition —
    // it pulls straight from statsByRarity (no formula/multiplier involved, unlike the
    // archetype-based WeaponGenerator.Generate) and rolls a rarity-sized subset of
    // possibleAbilities/possiblePassives (not a fixed final list — see
    // possibleAbilities' own doc comment for why).
    [CreateAssetMenu(menuName = "DoomScroller/Weapon Definition", fileName = "NewWeaponDefinition")]
    public class WeaponDefinition : ScriptableObject
    {
        public string weaponName;
        public WeaponType weaponType;
        public DamageType damageType;

        [Tooltip("Equipping this auto-unequips and locks the other hand slot, same as " +
                  "any other two-handed weapon.")]
        public bool isTwoHanded;

        public float attackSpeed = 1f;
        public Color itemColor = Color.gray;

        [Tooltip("Real icon art, if any — falls back to itemColor when unset. Copied onto " +
                  "every ItemData WeaponGenerator.GenerateFromDefinition produces.")]
        public Sprite icon;

        [Tooltip("Base damage and attribute-scaling for each rarity tier — always 5 " +
                  "entries, index 0..4 = Common/Uncommon/Rare/Legendary/Mythic. Fully " +
                  "hand-authored, not derived from a shared formula.")]
        public WeaponRarityStats[] statsByRarity = new WeaponRarityStats[5];

        [Tooltip("The pool this weapon can roll abilities from when generated — not its " +
                  "final ability list. GenerateFromDefinition draws a rarity-sized subset " +
                  "(Common 0, Uncommon 1, Rare 2, Legendary 3, Mythic 4) at random from here.")]
        public AbilityDefinition[] possibleAbilities;

        [Tooltip("The pool this weapon can roll a passive from when generated (independent " +
                  "chance, same as the archetype-based generator) — again a pool, not a " +
                  "guaranteed final passive.")]
        public PassiveDefinition[] possiblePassives;
    }
}
