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

        // Every (MaterialDefinition, ItemRarity) pair ever owned (not just currently held
        // — "seen" survives spending it), and every RecipeDefinition.recipeId ever
        // crafted — the two gates RecipeDefinition-based crafting checks (see
        // IsRecipeUnlocked). Unlike _knownPotionTypes above, both grow live via
        // AddToInventory/TryCraftRecipe, not just at construction — a material or recipe
        // can be discovered mid-session. Keyed by the pair, never family alone — owning
        // Common Wood doesn't discover Rare Wood.
        private readonly HashSet<(MaterialDefinition Material, ItemRarity Rarity)> _discoveredMaterials;
        private readonly HashSet<string> _craftedRecipeIds = new();

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
            _discoveredMaterials = new HashSet<(MaterialDefinition, ItemRarity)>(_inventory
                .Where(i => i.category == ItemCategory.Material && i.materialSource != null)
                .Select(i => (i.materialSource, i.rarity)));
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
            if (item.category == ItemCategory.Material && item.materialSource != null)
            {
                _discoveredMaterials.Add((item.materialSource, item.rarity));
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<ItemData> GetInventory(EquipmentSlotType slot)
        {
            if (IsHandSlot(slot))
            {
                // A weapon/shield isn't pinned to a specific hand — every owned item
                // whose category can occupy a hand slot at all is a candidate for
                // either hand, including one currently worn in the *other* hand
                // (equipping it here moves it — see Equip — rather than duplicating
                // it). Filtering by the item's own slotType here is what used to hide
                // roughly half the player's collection when browsing a single hand.
                return _inventory.Where(i => ItemCategoryInfo.SlotsFor(i.category).Contains(slot)).ToList();
            }

            // Potions and Materials never have a meaningful slotType (neither is ever
            // equipped to a body slot — see GetPotions/GetMaterials) so both are
            // excluded here regardless of whatever default value that field happens to
            // carry — Material in particular defaults to Head (EquipmentSlotType's
            // ordinal 0), since nothing ever sets it, which without this check would
            // wrongly surface every owned Material when browsing the Head slot.
            // ItemCategoryInfo.SlotsFor's own default case is empty for exactly these
            // two categories, so this one check covers both (and any future
            // non-equippable category) instead of hardcoding each by name.
            return _inventory.Where(i => i.slotType == slot && ItemCategoryInfo.SlotsFor(i.category).Length > 0).ToList();
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

        // Convenience overload for callers with no slot context of their own (the
        // starter-weapon grant, test fixtures) — equips into whatever slot the item's
        // own slotType already names. Hand-slot items browsed through
        // EquipSlotDetailView always go through the explicit-target overload below
        // instead, since a weapon/shield's slotType is no longer a reliable signal of
        // which hand the player actually wants (see GetInventory).
        public void Equip(ItemData item)
        {
            if (item == null)
            {
                return;
            }

            Equip(item, item.slotType);
        }

        public void Equip(ItemData item, EquipmentSlotType targetSlot)
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
            if (GetLockingTwoHandedWeapon(targetSlot) != null)
            {
                return;
            }

            // Both hands now browse the same weapon/shield pool (see GetInventory), so
            // this same item can appear as an option while it's still worn in the
            // other hand — tapping it there is a "move it to this hand" gesture, not a
            // duplicate-equip bug.
            if (IsHandSlot(targetSlot) && GetEquipped(OtherHandSlot(targetSlot)) == item)
            {
                _equipped.Remove(OtherHandSlot(targetSlot));
            }

            _equipped[targetSlot] = item;
            item.slotType = targetSlot;
            if (IsHandSlot(targetSlot) && IsTwoHanded(item))
            {
                // Equipping a two-hander clears whatever was in the other hand — it
                // can't coexist with anything else.
                _equipped.Remove(OtherHandSlot(targetSlot));
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

        // Every passive effect on any currently-equipped item, across every slot — not
        // weapon-only: EquipmentGenerator rolls the same passiveEffects field onto
        // Armor/Talismans, so a Talisman sitting in Talisman1/2 counts here too, same as
        // a weapon in either hand. Abilities (cast skills) stay weapon-only — see
        // GetEquippedWeaponAbilities() — this is only the always-on, no-cast side.
        public IEnumerable<StatusEffectData> GetEquippedPassiveEffects()
        {
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = GetEquipped(slot);
                if (item != null && item.passiveEffects != null)
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

        // potionFilter restricts which known potion types this considers — the Tavern
        // only ever refills Revive Draughts, not Max-Health potions (see
        // VillageTavernView).
        public bool ArePotionsFullyStocked(Func<ItemData, bool> potionFilter)
        {
            return _knownPotionTypes.Where(potionFilter).All(t => _inventory.Count(i => i == t) >= GetRefillCapacity(t));
        }

        // The flat total to fully refill everything missing right now — for a button
        // label that states one sum rather than a per-unit price.
        public int ComputeRefillCost(Func<ItemData, int> pricePerPotion, Func<ItemData, bool> potionFilter)
        {
            var total = 0;
            foreach (var potionType in _knownPotionTypes.Where(potionFilter))
            {
                var owned = _inventory.Count(i => i == potionType);
                var missing = Mathf.Max(0, GetRefillCapacity(potionType) - owned);
                total += missing * pricePerPotion(potionType);
            }

            return total;
        }

        // Tops every matching known potion type back up to its refill capacity (raised
        // per-type by an Alchemist capacity upgrade) — one potion at a time, stopping the
        // moment the wallet can't afford the next one (a partial refill, not
        // all-or-nothing). Returns how many were actually bought, for a confirmation
        // message; 0 if fully stocked already or the wallet couldn't afford even one.
        // pricePerPotion is per-type since a quality-upgraded Revive potion costs more.
        public int RefillPotions(PlayerWallet wallet, Func<ItemData, int> pricePerPotion, Func<ItemData, bool> potionFilter)
        {
            var added = 0;
            foreach (var potionType in _knownPotionTypes.Where(potionFilter))
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

        // Every owned item not currently equipped anywhere — gear, potions, and
        // materials alike (the Market's Sell tab draws directly from this).
        public IEnumerable<ItemData> GetSellableItems()
        {
            return _inventory.Where(i => !_equipped.Values.Contains(i));
        }

        // The Market's Sell tab action — mirrors TryCraftRecipe/TryUpgradeRevive*'s shape
        // (a combined inventory+wallet transaction living here, wallet passed in). Refuses
        // an equipped item the same way RemoveFromInventory already does.
        public bool SellItem(ItemData item, PlayerWallet wallet)
        {
            if (item == null || _equipped.Values.Contains(item) || !_inventory.Remove(item))
            {
                return false;
            }

            wallet.AddGold(item.goldValue);
            Changed?.Invoke();
            return true;
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

        public bool HasDiscovered(MaterialDefinition material, ItemRarity rarity) =>
            _discoveredMaterials.Contains((material, rarity));

        public bool HasCrafted(RecipeDefinition recipe) => recipe != null && _craftedRecipeIds.Contains(recipe.recipeId);

        private int CountOfMaterial(MaterialDefinition material, ItemRarity rarity) =>
            _inventory.Count(i => i.category == ItemCategory.Material && i.materialSource == material && i.rarity == rarity);

        // Whether the player currently holds enough of every named ingredient, AT the
        // given rarity — the same rarity that will actually be consumed (see
        // ConsumeNamedIngredients). Shared by recipes (HasIngredientsFor) and weapon
        // upgrades (TryEmpowerWeapon/TryRerollWeapon's own checks) — distinct from
        // IsRecipeUnlocked, which only checks discovery/prerequisites, not current
        // stock, since a recipe stays known even after its materials are spent.
        public bool HasIngredients(RecipeIngredient[] ingredients, ItemRarity rarity)
        {
            return (ingredients ?? Array.Empty<RecipeIngredient>()).All(i => CountOfMaterial(i.material, rarity) >= i.count);
        }

        // Exposed for the Blacksmith's confirm-dialog affordability check; TryCraftRecipe
        // re-checks this itself rather than trusting the caller already did. Crafting a
        // weapon/equipment of rarity X requires materials of rarity X (recipe.OutputRarity
        // — Common for a FixedItem recipe, which has no rarity of its own to match).
        public bool HasIngredientsFor(RecipeDefinition recipe) => HasIngredients(recipe.ingredients, recipe.OutputRarity);

        // True once every named ingredient has been discovered AT the recipe's own
        // output rarity (ever owned at that rarity — see HasDiscovered) and every
        // prerequisite recipe has already been crafted at least once (see HasCrafted).
        // Doesn't check current stock — that's TryCraftRecipe's job, since a recipe
        // stays "unlocked" (visible, known) even after its materials are spent.
        public bool IsRecipeUnlocked(RecipeDefinition recipe)
        {
            if (recipe.ingredients != null &&
                recipe.ingredients.Any(i => i.material == null || !HasDiscovered(i.material, recipe.OutputRarity)))
            {
                return false;
            }

            return recipe.prerequisiteRecipes == null || recipe.prerequisiteRecipes.All(HasCrafted);
        }

        private void ConsumeNamedIngredients(RecipeIngredient[] ingredients, ItemRarity rarity)
        {
            if (ingredients == null)
            {
                return;
            }

            foreach (var ingredient in ingredients)
            {
                var owned = _inventory
                    .Where(i => i.category == ItemCategory.Material && i.materialSource == ingredient.material && i.rarity == rarity)
                    .Take(ingredient.count)
                    .ToList();
                foreach (var item in owned)
                {
                    _inventory.Remove(item);
                }
            }
        }

        // The Blacksmith's recipe-tree craft action — named ingredients + discovery/
        // prerequisite gating (see IsRecipeUnlocked), goldCost pre-discounted by the
        // caller (same convention as TryUpgradeReviveCapacity/SellItem — pricing stays a
        // view concern, not an inventory one).
        public bool TryCraftRecipe(RecipeDefinition recipe, PlayerWallet wallet, int goldCost)
        {
            if (recipe == null || !IsRecipeUnlocked(recipe) || !HasIngredientsFor(recipe))
            {
                return false;
            }

            if (!wallet.TrySpendGold(goldCost))
            {
                return false;
            }

            ConsumeNamedIngredients(recipe.ingredients, recipe.OutputRarity);
            _craftedRecipeIds.Add(recipe.recipeId);
            AddToInventory(CreateRecipeOutput(recipe));
            return true;
        }

        // The Blacksmith's Upgrade tab actions — goldCost/ingredients are pre-resolved by
        // the caller (WeaponUpgrader.EmpowerGoldCost/EmpowerIngredients or their Reroll
        // equivalents), same pricing-stays-a-view-concern split as TryCraftRecipe. Both
        // mutate weapon in place via WeaponUpgrader rather than replacing the owned
        // reference (see WeaponUpgrader's own doc comment for why) — Changed fires so any
        // Blacksmith row/equip-slot display showing this weapon updates live.
        public bool TryEmpowerWeapon(ItemData weapon, PlayerWallet wallet, int goldCost, RecipeIngredient[] ingredients)
        {
            // Costs materials at the weapon's CURRENT rarity — empowering a Rare weapon
            // costs Rare materials, same rule as crafting a Rare weapon from scratch.
            if (!WeaponUpgrader.CanEmpower(weapon) || !HasIngredients(ingredients, weapon.rarity))
            {
                return false;
            }

            if (!wallet.TrySpendGold(goldCost))
            {
                return false;
            }

            ConsumeNamedIngredients(ingredients, weapon.rarity);
            WeaponUpgrader.Empower(weapon);
            Changed?.Invoke();
            return true;
        }

        public bool TryRerollWeapon(ItemData weapon, PlayerWallet wallet, int goldCost, RecipeIngredient[] ingredients)
        {
            if (weapon == null || weapon.category != ItemCategory.Weapon || !HasIngredients(ingredients, weapon.rarity))
            {
                return false;
            }

            if (!wallet.TrySpendGold(goldCost))
            {
                return false;
            }

            ConsumeNamedIngredients(ingredients, weapon.rarity);
            WeaponUpgrader.Reroll(weapon);
            Changed?.Invoke();
            return true;
        }

        // Builds the actual item a recipe grants — a fixed hand-authored ItemData as-is,
        // or a fresh roll of the authored WeaponDefinition/EquipmentDefinition at the
        // recipe's fixed outputRarity (see RecipeOutputKind's own doc comment for why
        // that rarity isn't randomized at craft time).
        private static ItemData CreateRecipeOutput(RecipeDefinition recipe)
        {
            return recipe.outputKind switch
            {
                RecipeOutputKind.Weapon => WeaponGenerator.GenerateFromDefinition(recipe.outputWeapon, recipe.outputRarity),
                RecipeOutputKind.Equipment => EquipmentGenerator.GenerateFromDefinition(recipe.outputEquipment, recipe.outputRarity),
                RecipeOutputKind.Material => MaterialGenerator.GenerateFromDefinition(recipe.outputMaterial, recipe.outputRarity),
                _ => recipe.outputItem
            };
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

        // Fills in data's item-related fields (itemPool, inventory, equipped,
        // knownPotionTypes, reviveUpgrades) for SaveSystem — see SaveData's own doc
        // comment for why every distinct ItemData reference is captured exactly once
        // (itemPool) with everything else referencing it by index, rather than each
        // owned copy capturing its own independent snapshot.
        public void CaptureInto(SaveData data)
        {
            var poolIndex = new Dictionary<ItemData, int>();
            data.itemPool.Clear();

            int Intern(ItemData item)
            {
                if (poolIndex.TryGetValue(item, out var existingIndex))
                {
                    return existingIndex;
                }

                var newIndex = data.itemPool.Count;
                data.itemPool.Add(ItemSnapshot.Capture(item));
                poolIndex[item] = newIndex;
                return newIndex;
            }

            data.inventory.Clear();
            foreach (var item in _inventory)
            {
                data.inventory.Add(Intern(item));
            }

            data.equipped.Clear();
            foreach (var kvp in _equipped)
            {
                data.equipped.Add(new EquippedEntry { slot = kvp.Key, itemIndex = Intern(kvp.Value) });
            }

            data.knownPotionTypes.Clear();
            foreach (var potion in _knownPotionTypes)
            {
                data.knownPotionTypes.Add(Intern(potion));
            }

            data.discoveredMaterials.Clear();
            foreach (var (material, rarity) in _discoveredMaterials)
            {
                data.discoveredMaterials.Add(new DiscoveredMaterialEntry { materialName = material.materialName, rarity = rarity });
            }

            data.craftedRecipeIds.Clear();
            data.craftedRecipeIds.AddRange(_craftedRecipeIds);

            data.reviveUpgrades.Clear();
            foreach (var potion in _reviveCapacityLevel.Keys.Union(_reviveQualityLevel.Keys))
            {
                data.reviveUpgrades.Add(new PotionUpgradeEntry
                {
                    itemIndex = Intern(potion),
                    capacityLevel = GetReviveCapacityLevel(potion),
                    qualityLevel = GetReviveQualityLevel(potion)
                });
            }
        }

        // The load path — rebuilds every collection from a saved itemPool, so N indices
        // pointing at the same pool entry resolve back to one shared recreated instance
        // (preserving the reference-equality stacking every owner of this data relies
        // on). Called once, right after construction, before anything subscribes to
        // Changed — deliberately doesn't fire it.
        public void RestoreFrom(SaveData data)
        {
            var pool = data.itemPool.Select(snapshot => snapshot.Restore()).ToArray();

            _inventory.Clear();
            foreach (var index in data.inventory)
            {
                _inventory.Add(pool[index]);
            }

            _equipped.Clear();
            foreach (var entry in data.equipped)
            {
                _equipped[entry.slot] = pool[entry.itemIndex];
            }

            _knownPotionTypes.Clear();
            foreach (var index in data.knownPotionTypes)
            {
                _knownPotionTypes.Add(pool[index]);
            }

            _discoveredMaterials.Clear();
            foreach (var entry in data.discoveredMaterials)
            {
                var material = Resources.LoadAll<MaterialDefinition>("MaterialDefinitions")
                    .FirstOrDefault(d => d.materialName == entry.materialName);
                if (material != null)
                {
                    _discoveredMaterials.Add((material, entry.rarity));
                }
            }

            _craftedRecipeIds.Clear();
            foreach (var recipeId in data.craftedRecipeIds)
            {
                _craftedRecipeIds.Add(recipeId);
            }

            _reviveCapacityLevel.Clear();
            _reviveQualityLevel.Clear();
            foreach (var upgrade in data.reviveUpgrades)
            {
                var potion = pool[upgrade.itemIndex];
                if (upgrade.capacityLevel > 0)
                {
                    _reviveCapacityLevel[potion] = upgrade.capacityLevel;
                }

                if (upgrade.qualityLevel > 0)
                {
                    _reviveQualityLevel[potion] = upgrade.qualityLevel;
                }
            }
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
