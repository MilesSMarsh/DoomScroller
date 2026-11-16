using System;
using System.Collections.Generic;

namespace DoomScroller.CombatCore
{
    // Plain-data view of one equipped item: the ItemSnapshot fields combat reads, and
    // nothing else. The server fills these from the stored profile JSON, so the
    // item-to-FightInput mapping lives here once instead of in Unity-side code.
    // Ordinals match the game's enums (ItemCategory, EquipmentSlotType, DamageType).
    [Serializable]
    public class LoadoutItem
    {
        public int slot;
        public int category;
        public int powerValue;
        public float attackSpeed;
        public bool isRanged;
        public int damageType;
        public ScalingInput[] scaling = new ScalingInput[0];
        public LoadoutAbility[] abilities = new LoadoutAbility[0];
        public PassiveInput[] passives = new PassiveInput[0];
    }

    [Serializable]
    public class LoadoutAbility
    {
        public string name;
        public int powerBonus;
        public float cooldownSeconds;
        public EffectInput[] onCastEffects = new EffectInput[0];
    }

    // Mirrors FightInputBuilder.BuildPlayer for items already reduced to plain data.
    public static class LoadoutMapper
    {
        public const int WeaponCategory = 1;
        public const int LeftHandSlot = 4;
        public const int RightHandSlot = 5;
        public const int SlotCount = 8;
        public const int AttributeCount = 4;

        // equipped: at most one item per slot. Missing attribute entries read as 0.
        public static PlayerSideInput BuildPlayer(IEnumerable<LoadoutItem> equipped, int[] attributes, int startHp)
        {
            var bySlot = new LoadoutItem[SlotCount];
            foreach (var item in equipped)
            {
                if (item != null && item.slot >= 0 && item.slot < SlotCount) bySlot[item.slot] = item;
            }

            var player = new PlayerSideInput { startHp = startHp, attributes = new int[AttributeCount] };
            for (var i = 0; i < AttributeCount && attributes != null && i < attributes.Length; i++)
            {
                player.attributes[i] = attributes[i];
            }

            var weaponItems = new List<LoadoutItem>();
            var passives = new List<PassiveInput>();
            for (var slot = 0; slot < SlotCount; slot++)
            {
                var item = bySlot[slot];
                if (item == null) continue;
                if (item.category != WeaponCategory) player.defense += item.powerValue;
                if (item.passives != null) passives.AddRange(item.passives);
                if (item.category == WeaponCategory && (slot == LeftHandSlot || slot == RightHandSlot)) weaponItems.Add(item);
            }

            var weapons = new WeaponInput[weaponItems.Count];
            var abilities = new List<AbilityInput>();
            for (var i = 0; i < weaponItems.Count; i++)
            {
                var w = weaponItems[i];
                weapons[i] = new WeaponInput
                {
                    power = w.powerValue,
                    attackSpeed = w.attackSpeed,
                    isRanged = w.isRanged,
                    damageType = w.damageType,
                    scaling = w.scaling ?? new ScalingInput[0]
                };
                if (w.abilities == null) continue;
                foreach (var a in w.abilities)
                {
                    abilities.Add(new AbilityInput
                    {
                        name = a.name,
                        weaponIndex = i,
                        powerBonus = a.powerBonus,
                        cooldownSeconds = a.cooldownSeconds,
                        onCastEffects = a.onCastEffects ?? new EffectInput[0]
                    });
                }
            }

            player.weapons = weapons;
            player.abilities = abilities.ToArray();
            player.passives = passives.ToArray();
            return player;
        }
    }
}
