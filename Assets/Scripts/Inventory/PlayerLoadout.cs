using System;
using System.Collections.Generic;
using System.Linq;

namespace DoomScroller.Inventory
{
    // Plain C# so equip/unequip/ability-toggle logic is testable without a running scene.
    public class PlayerLoadout
    {
        public const int MaxActiveAbilities = 2;

        private readonly List<ItemData> _inventory;
        private readonly Dictionary<EquipmentSlotType, ItemData> _equipped = new();
        private readonly HashSet<string> _activeAbilities = new();

        public event Action Changed;

        public PlayerLoadout(IEnumerable<ItemData> inventory)
        {
            _inventory = inventory.ToList();
        }

        // Granted rewards (Phase 6) land here, so they're immediately equippable —
        // GetInventory/CycleEquipped always re-read this list fresh, no extra wiring needed.
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

        public ItemData GetEquipped(EquipmentSlotType slot)
        {
            return _equipped.TryGetValue(slot, out var item) ? item : null;
        }

        public void Equip(ItemData item)
        {
            if (item == null || !_inventory.Contains(item))
            {
                return;
            }

            _equipped[item.slotType] = item;
            if (item.slotType == EquipmentSlotType.Weapon)
            {
                PruneActiveAbilitiesToCurrentWeapon();
            }

            Changed?.Invoke();
        }

        public void Unequip(EquipmentSlotType slot)
        {
            if (!_equipped.Remove(slot))
            {
                return;
            }

            if (slot == EquipmentSlotType.Weapon)
            {
                _activeAbilities.Clear();
            }

            Changed?.Invoke();
        }

        // Cycles: None -> item1 -> item2 -> ... -> None, so tapping a slot both equips and unequips.
        public void CycleEquipped(EquipmentSlotType slot)
        {
            var options = _inventory.Where(i => i.slotType == slot).ToList();
            if (options.Count == 0)
            {
                return;
            }

            var current = GetEquipped(slot);
            var currentIndex = current == null ? -1 : options.IndexOf(current);
            var nextIndex = currentIndex + 1;

            if (nextIndex >= options.Count)
            {
                Unequip(slot);
            }
            else
            {
                Equip(options[nextIndex]);
            }
        }

        public bool IsAbilityActive(string abilityName)
        {
            return _activeAbilities.Contains(abilityName);
        }

        public void ToggleAbility(string abilityName)
        {
            var weapon = GetEquipped(EquipmentSlotType.Weapon);
            if (weapon == null || weapon.abilities.All(a => a.abilityName != abilityName))
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

        private void PruneActiveAbilitiesToCurrentWeapon()
        {
            var weapon = GetEquipped(EquipmentSlotType.Weapon);
            var validNames = weapon.abilities.Select(a => a.abilityName).ToHashSet();
            _activeAbilities.RemoveWhere(name => !validNames.Contains(name));
        }
    }
}
