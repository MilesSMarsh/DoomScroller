using System;
using System.Collections.Generic;
using System.Linq;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // Plain C# so equip/unequip logic is testable without a running scene.
    public class PlayerLoadout
    {
        private static readonly EquipmentSlotType[] HandSlots =
        {
            EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand
        };

        private readonly List<ItemData> _inventory;
        private readonly Dictionary<EquipmentSlotType, ItemData> _equipped = new();

        // The one revive potion "carried" into the next fight(s) — deliberately
        // simpler than the multi-slot active-ability system, since only one can ever
        // matter within a single fight (see CombatPlaybackView.Play).
        private ItemData _activeRevivePotion;

        public event Action Changed;

        // Fixed placeholder appearance for the vertical slice — not a piece of equipment,
        // so it lives here rather than in the inventory/slot system.
        public Sprite PlayerSprite { get; }

        public PlayerLoadout(IEnumerable<ItemData> inventory, Sprite playerSprite = null)
        {
            _inventory = inventory.ToList();
            PlayerSprite = playerSprite;
        }

        // Granted rewards (Phase 6) land here, so they're immediately equippable —
        // GetInventory/GetAllInventory always re-read this list fresh, no extra wiring needed.
        public void AddToInventory(ItemData item)
        {
            if (item == null)
            {
                return;
            }

            _inventory.Add(item);
            Changed?.Invoke();
        }

        public IReadOnlyList<ItemData> GetInventory(EquipmentSlotType slot)
        {
            // Potions never have a meaningful slotType (they're never equipped to a
            // body slot — see GetPotions) so they're excluded here regardless of
            // whatever default value that field happens to carry.
            return _inventory.Where(i => i.slotType == slot && i.category != ItemCategory.Potion).ToList();
        }

        // Unfiltered — the bottom-sheet inventory grid shows everything the player owns,
        // not just what matches whichever slot happened to be tapped.
        public IReadOnlyList<ItemData> GetAllInventory()
        {
            return _inventory.ToList();
        }

        public ItemData GetEquipped(EquipmentSlotType slot)
        {
            return _equipped.TryGetValue(slot, out var item) ? item : null;
        }

        // Permanently removes an item from the inventory. Refuses anything currently
        // equipped in any slot — the UI only ever offers deletion for unequipped items,
        // but this keeps that an invariant of the class itself rather than trusting
        // every caller to check first.
        public bool RemoveFromInventory(ItemData item)
        {
            if (item == null || _equipped.Values.Contains(item))
            {
                return false;
            }

            var removed = _inventory.Remove(item);
            if (removed)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        public void Equip(ItemData item)
        {
            // Potions are never equipped to a body slot — they're drunk (MaxHealthRestore)
            // or carried into a fight (Revive, see ToggleRevivePotionActive).
            if (item == null || item.category == ItemCategory.Potion || !_inventory.Contains(item))
            {
                return;
            }

            // A two-handed weapon in the other hand locks this slot — refuse the equip
            // rather than letting it silently coexist with a weapon that's supposed to
            // occupy both hands.
            if (GetLockingTwoHandedWeapon(item.slotType) != null)
            {
                return;
            }

            _equipped[item.slotType] = item;
            if (IsHandSlot(item.slotType) && IsTwoHanded(item))
            {
                // Equipping a two-hander clears whatever was in the other hand — it
                // can't coexist with anything else.
                _equipped.Remove(OtherHandSlot(item.slotType));
            }

            Changed?.Invoke();
        }

        // Non-null (and equal to the blocking weapon) when `slot` is a hand slot whose
        // opposite hand holds a two-handed weapon — meaning `slot` is locked: nothing can
        // be equipped into it, and the UI should render it as disabled. Null for
        // non-hand slots and whenever the opposite hand doesn't hold a two-hander.
        public ItemData GetLockingTwoHandedWeapon(EquipmentSlotType slot)
        {
            if (!IsHandSlot(slot))
            {
                return null;
            }

            var otherItem = GetEquipped(OtherHandSlot(slot));
            return IsTwoHanded(otherItem) ? otherItem : null;
        }

        public void Unequip(EquipmentSlotType slot)
        {
            if (!_equipped.Remove(slot))
            {
                return;
            }

            Changed?.Invoke();
        }

        // All abilities on any currently-equipped weapon, across both hands (dual-wielding
        // two weapons pools both weapons' abilities together — there's no single "the"
        // weapon slot anymore).
        public IEnumerable<WeaponAbilityData> GetEquippedWeaponAbilities()
        {
            foreach (var slot in HandSlots)
            {
                var item = GetEquipped(slot);
                if (item != null && item.category == ItemCategory.Weapon && item.abilities != null)
                {
                    foreach (var ability in item.abilities)
                    {
                        yield return ability;
                    }
                }
            }
        }

        // Same pairing as GetEquippedWeaponAbilities(), but keeps each ability's owning
        // weapon alongside it — CombatSimulator needs the weapon to compute an ability's
        // scaling bonus (the same stat(s)/coefficients its basic attack scales off).
        public IEnumerable<(ItemData Weapon, WeaponAbilityData Ability)> GetEquippedWeaponAbilityPairs()
        {
            foreach (var slot in HandSlots)
            {
                var item = GetEquipped(slot);
                if (item != null && item.category == ItemCategory.Weapon && item.abilities != null)
                {
                    foreach (var ability in item.abilities)
                    {
                        yield return (item, ability);
                    }
                }
            }
        }

        // Every passive effect on any currently-equipped weapon, across both hands — same
        // pairing convention as GetEquippedWeaponAbilities(), just for the always-on
        // (no cast required) side of a weapon's kit.
        public IEnumerable<StatusEffectData> GetEquippedPassiveWeaponEffects()
        {
            foreach (var slot in HandSlots)
            {
                var item = GetEquipped(slot);
                if (item != null && item.category == ItemCategory.Weapon && item.passiveEffects != null)
                {
                    foreach (var effect in item.passiveEffects)
                    {
                        yield return effect;
                    }
                }
            }
        }

        public IEnumerable<ItemData> GetPotions()
        {
            return _inventory.Where(i => i.category == ItemCategory.Potion);
        }

        public bool IsRevivePotionActive(ItemData potion)
        {
            return _activeRevivePotion == potion;
        }

        // Only one revive potion can be carried at a time — toggling a different one
        // replaces whichever was active, toggling the active one off clears it.
        public void ToggleRevivePotionActive(ItemData potion)
        {
            if (potion == null || potion.potionEffect != PotionEffect.Revive || !_inventory.Contains(potion))
            {
                return;
            }

            _activeRevivePotion = _activeRevivePotion == potion ? null : potion;
            Changed?.Invoke();
        }

        // Null once consumed, unequipped from carry, or no longer in the inventory.
        public ItemData GetActiveRevivePotion()
        {
            return _activeRevivePotion != null && _inventory.Contains(_activeRevivePotion) ? _activeRevivePotion : null;
        }

        // Spent by CombatPlaybackView when a carried revive potion actually saves the
        // player — removed from the inventory and no longer carried.
        public void ConsumeActiveRevivePotion()
        {
            if (_activeRevivePotion == null)
            {
                return;
            }

            _inventory.Remove(_activeRevivePotion);
            _activeRevivePotion = null;
            Changed?.Invoke();
        }

        // Instant-use from the Potions tab — restores current max health right away,
        // rather than being carried into a fight like a revive potion.
        public void ConsumeMaxHealthPotion(ItemData potion, PlayerHealth health)
        {
            if (potion == null || potion.potionEffect != PotionEffect.MaxHealthRestore || !_inventory.Contains(potion))
            {
                return;
            }

            health.RestoreMaxHealth(potion.potionAmount);
            _inventory.Remove(potion);
            Changed?.Invoke();
        }

        private static bool IsHandSlot(EquipmentSlotType slot)
        {
            return slot == EquipmentSlotType.LeftHand || slot == EquipmentSlotType.RightHand;
        }

        private static EquipmentSlotType OtherHandSlot(EquipmentSlotType slot)
        {
            return slot == EquipmentSlotType.LeftHand ? EquipmentSlotType.RightHand : EquipmentSlotType.LeftHand;
        }

        private static bool IsTwoHanded(ItemData item)
        {
            return item != null && item.category == ItemCategory.Weapon && item.isTwoHanded;
        }
    }
}
