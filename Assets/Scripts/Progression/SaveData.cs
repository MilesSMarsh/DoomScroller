using System;
using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // A plain (non-UnityEngine.Object) mirror of every ItemData field except StatLabel
    // (display-only, recomputed from category). JsonUtility can't nest UnityEngine.Object
    // references, so an owned/equipped item is captured as one of these instead of the
    // live ItemData — see SaveData.itemPool for why every distinct reference is captured
    // exactly once rather than once per owned copy.
    [Serializable]
    public class ItemSnapshot
    {
        // When set, Restore() returns the ORIGINAL singleton ItemData asset (looked up by
        // this id) instead of a fresh clone — required for anything a static design-time
        // reference points at that must survive a save/reload (currently
        // RecipeIngredient.material; see ItemData.itemId's own doc comment). Blank for
        // procedurally-rolled items, which fall back to the clone-from-snapshot behavior
        // every item used before this field existed.
        public string itemId;

        public string title;
        public EquipmentSlotType slotType;
        public ItemCategory category;
        public ItemRarity rarity;
        public string statSummary;
        public Color itemColor;
        public WeaponType weaponType;
        public DamageType damageType;
        public int goldValue;
        public int powerValue;
        public WeaponAbilityData[] abilities;
        public StatusEffectData[] passiveEffects;
        public bool isTwoHanded;
        public float attackSpeed;
        public bool isRanged;
        public PotionEffect potionEffect;
        public int potionAmount;
        public AttributeScaling[] attributeScalingEntries;

        // ItemData.sourceDefinition is a UnityEngine.Object reference — JsonUtility can't
        // carry that across a save, so it's captured by WeaponDefinition.weaponName
        // instead (same "look it up by a stable string" convention as itemId above) and
        // re-resolved against the live WeaponDefinitions catalog on Restore. Blank for an
        // archetype-rolled weapon, which never had a source definition to begin with.
        public string sourceWeaponName;

        // Same convention as sourceWeaponName above, but for ItemData.materialSource —
        // every Material-category item has one (see MaterialGenerator.GenerateFromDefinition),
        // re-resolved against the live MaterialDefinitions catalog on Restore.
        public string sourceMaterialName;

        // Same convention again, for ItemData.equipmentSource (EquipmentDefinition.
        // equipmentName). Blank for anything not rolled from an EquipmentDefinition.
        public string sourceEquipmentName;

        public static ItemSnapshot Capture(ItemData item)
        {
            return new ItemSnapshot
            {
                itemId = item.itemId,
                title = item.title,
                slotType = item.slotType,
                category = item.category,
                rarity = item.rarity,
                statSummary = item.statSummary,
                itemColor = item.itemColor,
                weaponType = item.weaponType,
                damageType = item.damageType,
                goldValue = item.goldValue,
                powerValue = item.powerValue,
                abilities = item.abilities,
                passiveEffects = item.passiveEffects,
                isTwoHanded = item.isTwoHanded,
                attackSpeed = item.attackSpeed,
                isRanged = item.isRanged,
                potionEffect = item.potionEffect,
                potionAmount = item.potionAmount,
                attributeScalingEntries = item.attributeScalingEntries,
                sourceWeaponName = item.sourceDefinition != null ? item.sourceDefinition.weaponName : null,
                sourceMaterialName = item.materialSource != null ? item.materialSource.materialName : null,
                sourceEquipmentName = item.equipmentSource != null ? item.equipmentSource.equipmentName : null
            };
        }

        public ItemData Restore()
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                var original = Resources.LoadAll<ItemData>("Items").FirstOrDefault(i => i.itemId == itemId);
                if (original != null)
                {
                    return original;
                }
            }

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = itemId;
            item.title = title;
            item.slotType = slotType;
            item.category = category;
            item.rarity = rarity;
            item.statSummary = statSummary;
            item.itemColor = itemColor;
            item.weaponType = weaponType;
            item.damageType = damageType;
            item.goldValue = goldValue;
            item.powerValue = powerValue;
            item.abilities = abilities;
            item.passiveEffects = passiveEffects;
            item.isTwoHanded = isTwoHanded;
            item.attackSpeed = attackSpeed;
            item.isRanged = isRanged;
            item.potionEffect = potionEffect;
            item.potionAmount = potionAmount;
            item.attributeScalingEntries = attributeScalingEntries;
            if (!string.IsNullOrEmpty(sourceWeaponName))
            {
                item.sourceDefinition = Resources.LoadAll<WeaponDefinition>("WeaponDefinitions")
                    .FirstOrDefault(d => d.weaponName == sourceWeaponName);
            }

            if (!string.IsNullOrEmpty(sourceMaterialName))
            {
                item.materialSource = Resources.LoadAll<MaterialDefinition>("MaterialDefinitions")
                    .FirstOrDefault(d => d.materialName == sourceMaterialName);
            }

            if (!string.IsNullOrEmpty(sourceEquipmentName))
            {
                item.equipmentSource = Resources.LoadAll<EquipmentDefinition>("EquipmentDefinitions")
                    .FirstOrDefault(d => d.equipmentName == sourceEquipmentName);
            }

            // Sprites can't ride through JsonUtility either, so the icon is re-read off
            // whichever definition was resolved above — which also means art assigned
            // after the item was saved shows up on already-owned copies.
            if (item.materialSource != null)
            {
                item.icon = item.materialSource.icon;
            }
            else if (item.sourceDefinition != null)
            {
                item.icon = item.sourceDefinition.icon;
            }
            else if (item.equipmentSource != null)
            {
                item.icon = item.equipmentSource.icon;
            }

            return item;
        }
    }

    [Serializable]
    public class EquippedEntry
    {
        public EquipmentSlotType slot;
        public int itemIndex;
    }

    // One (family, rarity) pair the player has ever owned — MaterialDefinition is a
    // persistent Resources asset like WeaponDefinition, so it's captured by its stable
    // materialName (same "look it up by a stable string" convention as
    // ItemSnapshot.sourceWeaponName) rather than an itemPool index; there's no ItemData
    // instance to intern here, just the (definition, rarity) pair itself.
    [Serializable]
    public class DiscoveredMaterialEntry
    {
        public string materialName;
        public ItemRarity rarity;
    }

    // One entry per potion type with a nonzero Alchemist upgrade — merges
    // PlayerLoadout's two parallel dictionaries (_reviveCapacityLevel/_reviveQualityLevel)
    // into a single list keyed by itemPool index.
    [Serializable]
    public class PotionUpgradeEntry
    {
        public int itemIndex;
        public int capacityLevel;
        public int qualityLevel;
    }

    // Everything persisted across a session. itemPool holds every distinct ItemData
    // reference the loadout currently touches (owned, equipped, known-potion-type, or
    // revive-upgraded) exactly once; every other list below references it by index so
    // that N owned copies of the same item resolve back to the same recreated instance
    // on load — required for the reference-equality stacking used throughout the UI
    // (ProfileView.RefreshMaterials, PotionInventoryView, VillageMarketView's Sell tab).
    [Serializable]
    public class SaveData
    {
        public List<ItemSnapshot> itemPool = new();
        public List<int> inventory = new();
        public List<EquippedEntry> equipped = new();
        public List<int> knownPotionTypes = new();
        public List<PotionUpgradeEntry> reviveUpgrades = new();

        // Materials ever owned, by (family, rarity) — see DiscoveredMaterialEntry — and
        // RecipeDefinition.recipeId values ever crafted — see
        // PlayerLoadout.IsRecipeUnlocked for what these gate.
        public List<DiscoveredMaterialEntry> discoveredMaterials = new();
        public List<string> craftedRecipeIds = new();

        public int progressionLevel;
        public int progressionCurrentXp;

        public int healthTrueMax;
        public int healthCurrentMax;
        public long healthLastRegenUtcTicks;

        public int[] attributeValues;
        public int attributeUnspentPoints;

        public int gold;

        public bool autoConsumeRevivePotions;
        public bool manualAbilityCasting;
    }
}
