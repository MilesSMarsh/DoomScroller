# 004 — New game starts with a generated Common Wooden Sword

**Owner:** gameplay-engineer
**Size:** S
**Status:** review
**GDD:** docs/gdd/GDD.md#first-session (bug fix: a new game must start armed)
**Depends on:** none

## Goal
A new game starts with a Common Wooden Sword equipped. It's rolled from the authored
definition like any other weapon. Existing saves keep the loadout they already have.

## Acceptance criteria
- [x] `ProfileController`'s `[SerializeField] ItemData starterWeapon` is replaced by
      `[SerializeField] WeaponDefinition starterWeaponDefinition`. Keep the tooltip's intent
      and update its wording.
- [x] Starter creation lives in a plain static method outside the MonoBehaviour (e.g.
      `StarterWeapon.Create(WeaponDefinition definition, System.Random rng)` in
      `Assets/Scripts/Progression/`, namespace `DoomScroller.Progression`). It calls
      `WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Common, rng)` and
      returns null for a null definition. It never calls the archetype `Generate`.
- [x] `EquipStarterWeapon()` runs only from `StartFresh()`. It equips the generated item
      and logs a warning (not an exception) if the definition is unassigned.
      `LoadFromSave` is unchanged, so existing saves keep their loadout.
- [x] EditMode tests (new `Assets/Tests/EditMode/StarterWeaponTests.cs`) load
      `Resources.Load<WeaponDefinition>("WeaponDefinitions/Wooden Sword")` and assert:
      - the asset exists;
      - the result is non-null, `category == Weapon`, `rarity == Common`,
        `sourceDefinition == definition`, `title == "Wooden Sword"`, and
        `powerValue == statsByRarity[0].baseDamage`;
      - it has no abilities or passives (`SkillCountByRarity[0] == 0`);
      - the same seed gives the same result;
      - a null definition returns null.
- [x] EditMode test: the generated starter round-trips through
      `ItemSnapshot` / `SaveData` and re-links `sourceDefinition` to Wooden Sword by
      name, so it persists after the first autosave.
- [x] Full EditMode suite passes.
- [x] In SampleScene, `ProfileController.starterWeaponDefinition` points at
      `Assets/Resources/WeaponDefinitions/Wooden Sword.asset`
      (guid `1cc64e9a05244643ea6a4dc8b088eeb7`). Set it with Unity MCP after the code
      compiles, save the scene, and make sure SampleScene is still the open scene
      afterwards.
- [ ] (manual, Miles) Delete `savegame.json` from `Application.persistentDataPath` and
      press Play. The Profile shows a Common Wooden Sword equipped and fights deal damage.
- [ ] (manual, Miles) With an existing save, press Play. The loadout is unchanged and
      no extra Wooden Sword is added.

## Context
- Memory: memory/systems.md (Profile and progression, Loot and items, Save and persistence)
- May read: `Assets/Scripts/Inventory/ProfileController.cs`,
  `Assets/Scripts/Inventory/WeaponDefinition.cs`, `Assets/Scripts/Progression/WeaponGenerator.cs`,
  `Assets/Scripts/Progression/SaveData.cs`, `Assets/Scripts/Inventory/PlayerLoadout.cs`,
  `Assets/Resources/WeaponDefinitions/Wooden Sword.asset`,
  `Assets/Tests/EditMode/TestFixtures.cs`, `Assets/Tests/EditMode/StoneSwordIntegrationTests.cs`
  (pattern for loading a definition in tests)
- May write: `Assets/Scripts/Inventory/ProfileController.cs`,
  new `Assets/Scripts/Progression/StarterWeapon.cs`,
  new `Assets/Tests/EditMode/StarterWeaponTests.cs` (+ `.meta` files),
  `Assets/Scenes/SampleScene.unity` (via Unity MCP only)

## Out of scope
- Rebalancing Wooden Sword stats or adding an icon (its `icon` is unset; the existing
  placeholder rendering is fine).
- Giving existing saves a weapon, or migrating saves that have no weapon.
- First-session scripted cards, starting potions, or any other starting inventory.
- Changing `startingInventory`.

## Notes
- The old field already points at a missing asset, so changing its type loses nothing.
  The save shape is unchanged because the starter is a normal generated `ItemData`.
- Common has 0 skill slots, so the rng doesn't change the result today. Still pass an
  injected `System.Random` for consistency with the conventions.
- The Unity Editor must be open for the MCP scene edit. Use MCP to run tests too if the
  headless script exits 2.
