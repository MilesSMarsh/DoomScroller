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

        // Which potion types the Village's Tavern can refill — the exact set the player
        // started the game with. Captured once at construction since there's no
        // shop/discovery system yet to grow this list later (see RefillPotions).
        private readonly List<ItemData> _knownPotionTypes;
        private const int BaseRefillCapacity = 3;

        // The Alchemist's two upgrade tracks for a Revive potion, keyed by which potion
        // type (there's only ever one in practice today — see GetKnownRevivePotionType).
        // Never mutates the shared ItemData asset itself; both are read as a bonus layered
        // on top of it (GetRefillCapacity, GetRevivePotionAmount).
        public const int MaxUpgradeLevel = 5;
        private const int ReviveQualityBonusPerLevel = 10;
        private readonly Dictionary<ItemData, int> _reviveCapacityLevel = new();
        private readonly Dictionary<ItemData, int> _reviveQualityLevel = new();

        public event Action Changed;

        // Fixed placeholder appearance for the vertical slice — not a piece of equipment,
        // so it lives here rather than in the inventory/slot system.
        public Sprite PlayerSprite { get; }

        public PlayerLoadout(IEnumerable<ItemData> inventory, Sprite playerSprite = null)
        {
            _inventory = inventory.ToList();
            _knownPotionTypes = _inventory.Where(i => i.category == ItemCategory.Potion).Distinct().ToList();
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
            // or spent automatically on defeat (Revive, see GetBestRevivePotion).
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

        // The default for every potion type, except a Revive potion whose capacity has
        // been raised at the Alchemist.
        public int GetRefillCapacity(ItemData potionType)
        {
            return potionType.potionEffect == PotionEffect.Revive
                ? BaseRefillCapacity + GetReviveCapacityLevel(potionType)
                : BaseRefillCapacity;
        }

        public bool ArePotionsFullyStocked()
        {
            return _knownPotionTypes.All(t => _inventory.Count(i => i == t) >= GetRefillCapacity(t));
        }

        // The flat total to fully refill everything missing right now — for a button
        // label that states one sum rather than a per-unit price.
        public int ComputeRefillCost(Func<ItemData, int> pricePerPotion)
        {
            var total = 0;
            foreach (var potionType in _knownPotionTypes)
            {
                var owned = _inventory.Count(i => i == potionType);
                var missing = Mathf.Max(0, GetRefillCapacity(potionType) - owned);
                total += missing * pricePerPotion(potionType);
            }

            return total;
        }

        // Tops every potion type the player started with back up to its refill capacity
        // (raised per-type by an Alchemist capacity upgrade) — one potion at a time,
        // stopping the moment the wallet can't afford the next one (a partial refill, not
        // all-or-nothing). Returns how many were actually bought, for a confirmation
        // message; 0 if fully stocked already or the wallet couldn't afford even one.
        // pricePerPotion is per-type since a quality-upgraded Revive potion costs more.
        public int RefillPotions(PlayerWallet wallet, Func<ItemData, int> pricePerPotion)
        {
            var added = 0;
            foreach (var potionType in _knownPotionTypes)
            {
                var owned = _inventory.Count(i => i == potionType);
                var price = pricePerPotion(potionType);
                for (var i = owned; i < GetRefillCapacity(potionType); i++)
                {
                    if (!wallet.TrySpendGold(price))
                    {
                        if (added > 0)
                        {
                            Changed?.Invoke();
                        }

                        return added;
                    }

                    _inventory.Add(potionType);
                    added++;
                }
            }

            if (added > 0)
            {
                Changed?.Invoke();
            }

            return added;
        }

        // The strongest owned Revive potion (highest *effective* amount, including any
        // Alchemist quality upgrade) — null if none owned. Every Revive potion is always
        // available; there's nothing to pre-select any more (see CombatPlaybackView.Play).
        public ItemData GetBestRevivePotion()
        {
            return _inventory.Where(i => i.potionEffect == PotionEffect.Revive)
                .OrderByDescending(GetRevivePotionAmount)
                .FirstOrDefault();
        }

        // Spent by CombatPlaybackView when a Revive potion actually saves the player.
        public void ConsumeRevivePotion(ItemData potion)
        {
            if (potion == null || !_inventory.Remove(potion))
            {
                return;
            }

            Changed?.Invoke();
        }

        public int GetReviveCapacityLevel(ItemData potion)
        {
            return _reviveCapacityLevel.TryGetValue(potion, out var level) ? level : 0;
        }

        public int GetReviveQualityLevel(ItemData potion)
        {
            return _reviveQualityLevel.TryGetValue(potion, out var level) ? level : 0;
        }

        // The actual HP a revive potion restores — base amount plus quality upgrades.
        // Never mutates the shared ItemData asset itself, just layers a player-specific
        // bonus on top of it.
        public int GetRevivePotionAmount(ItemData potion)
        {
            return potion.potionAmount + GetReviveQualityLevel(potion) * ReviveQualityBonusPerLevel;
        }

        // The one Revive-type potion this player has ever owned — there's only ever one
        // in practice today; the Alchemist's upgrades operate on whichever this returns.
        // Null (the Alchemist shows "no revive potions discovered yet") if none.
        public ItemData GetKnownRevivePotionType()
        {
            return _knownPotionTypes.FirstOrDefault(p => p.potionEffect == PotionEffect.Revive);
        }

        public int CountMaterials()
        {
            return _inventory.Count(i => i.category == ItemCategory.Material);
        }

        public IEnumerable<ItemData> GetMaterials()
        {
            return _inventory.Where(i => i.category == ItemCategory.Material);
        }

        private void ConsumeMaterials(int count)
        {
            foreach (var material in _inventory.Where(i => i.category == ItemCategory.Material).Take(count).ToList())
            {
                _inventory.Remove(material);
            }
        }

        // goldCost/materialsCost are the Alchemist view's call (same split as
        // RewardGenerator/MarketGenerator owning prices while PlayerLoadout just executes
        // the spend) — this only enforces affordability and MaxUpgradeLevel, atomically
        // (nothing is spent when it returns false).
        public bool TryUpgradeReviveCapacity(ItemData potion, PlayerWallet wallet, int goldCost, int materialsCost)
        {
            if (potion == null || GetReviveCapacityLevel(potion) >= MaxUpgradeLevel || CountMaterials() < materialsCost)
            {
                return false;
            }

            if (!wallet.TrySpendGold(goldCost))
            {
                return false;
            }

            ConsumeMaterials(materialsCost);
            _reviveCapacityLevel[potion] = GetReviveCapacityLevel(potion) + 1;
            Changed?.Invoke();
            return true;
        }

        public bool TryUpgradeReviveQuality(ItemData potion, PlayerWallet wallet, int goldCost, int materialsCost)
        {
            if (potion == null || GetReviveQualityLevel(potion) >= MaxUpgradeLevel || CountMaterials() < materialsCost)
            {
                return false;
            }

            if (!wallet.TrySpendGold(goldCost))
            {
                return false;
            }

            ConsumeMaterials(materialsCost);
            _reviveQualityLevel[potion] = GetReviveQualityLevel(potion) + 1;
            Changed?.Invoke();
            return true;
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
