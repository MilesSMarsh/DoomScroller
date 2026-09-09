using System;
using System.Collections.Generic;
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
        public string title;
        public EquipmentSlotType slotType;
        public ItemCategory category;
        public string statSummary;
        public Color itemColor;
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

        public static ItemSnapshot Capture(ItemData item)
        {
            return new ItemSnapshot
            {
                title = item.title,
                slotType = item.slotType,
                category = item.category,
                statSummary = item.statSummary,
                itemColor = item.itemColor,
                goldValue = item.goldValue,
                powerValue = item.powerValue,
                abilities = item.abilities,
                passiveEffects = item.passiveEffects,
                isTwoHanded = item.isTwoHanded,
                attackSpeed = item.attackSpeed,
                isRanged = item.isRanged,
                potionEffect = item.potionEffect,
                potionAmount = item.potionAmount,
                attributeScalingEntries = item.attributeScalingEntries
            };
        }

        public ItemData Restore()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = title;
            item.slotType = slotType;
            item.category = category;
            item.statSummary = statSummary;
            item.itemColor = itemColor;
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
            return item;
        }
    }

    [Serializable]
    public class EquippedEntry
    {
        public EquipmentSlotType slot;
        public int itemIndex;
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
