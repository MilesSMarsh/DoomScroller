using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // Plain C# so equip/unequip/ability-toggle logic is testable without a running scene.
    public class PlayerLoadout
    {
        public const int MaxActiveAbilities = 2;

        private static readonly EquipmentSlotType[] HandSlots =
        {
            EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand
        };

        private readonly List<ItemData> _inventory;
        private readonly Dictionary<EquipmentSlotType, ItemData> _equipped = new();
        private readonly HashSet<string> _activeAbilities = new();

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
            return _inventory.Where(i => i.slotType == slot).ToList();
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
            if (item == null || !_inventory.Contains(item))
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
            if (IsHandSlot(item.slotType))
            {
                if (IsTwoHanded(item))
                {
                    // Equipping a two-hander clears whatever was in the other hand —
                    // it can't coexist with anything else.
                    _equipped.Remove(OtherHandSlot(item.slotType));
                }

                PruneActiveAbilitiesToEquippedWeapons();
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

            if (IsHandSlot(slot))
            {
                PruneActiveAbilitiesToEquippedWeapons();
            }

            Changed?.Invoke();
        }

        public bool IsAbilityActive(string abilityName)
        {
            return _activeAbilities.Contains(abilityName);
        }

        // Passive abilities are always effective; active ones only while toggled on.
        // Shared by CombatResolver and the abilities UI so this branch lives in one place.
        public bool IsAbilityEffective(WeaponAbilityData ability)
        {
            return !ability.isActive || IsAbilityActive(ability.abilityName);
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

        public void ToggleAbility(string abilityName)
        {
            var ability = GetEquippedWeaponAbilities().FirstOrDefault(a => a.abilityName == abilityName);
            if (ability == null || !ability.isActive)
            {
                return;
            }

            if (_activeAbilities.Contains(abilityName))
            {
                _activeAbilities.Remove(abilityName);
            }
            else if (_activeAbilities.Count < MaxActiveAbilities)
            {
                _activeAbilities.Add(abilityName);
            }
            else
            {
                return;
            }

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

        private void PruneActiveAbilitiesToEquippedWeapons()
        {
            var validNames = GetEquippedWeaponAbilities().Select(a => a.abilityName).ToHashSet();
            _activeAbilities.RemoveWhere(name => !validNames.Contains(name));
        }
    }
}
