# 005 — Potion ItemData assets and starting potions

**Owner:** content-designer
**Size:** S
**Status:** review
**GDD:** docs/gdd/GDD.md#combat (Restoration / Revive potions), #progression
**Depends on:** 004 for the SampleScene edit only (both edit the same `ProfileController`
component; don't run them at the same time). The asset work can start now.

## Goal
A new game knows both potion types the GDD names, so Revive can save a losing fight, the
Tavern can refill Revive potions, the Alchemist can upgrade them, and the Potions tab can
drink a Restoration potion.

## Acceptance criteria
- [ ] `Assets/Resources/Items/Potion_Revive.asset` exists: `category` Potion, `potionEffect`
      Revive, non-empty unique `itemId` (`potion_revive`), `title` "Revive Draught",
      `potionAmount` 30 (the revive HP percentage, decided; see Notes), `goldValue`,
      `rarity` Common, placeholder `itemColor`, no icon.
- [ ] `Assets/Resources/Items/Potion_Restoration.asset` exists: `category` Potion,
      `potionEffect` MaxHealthRestore, `itemId` `potion_restoration`, `title`
      "Restoration Potion", `potionAmount` (max HP restored), `goldValue`, rarity Common,
      placeholder colour.
- [ ] Both have committed `.meta` files; `Resources.LoadAll<ItemData>("Items")` returns
      exactly these two (no other assets in the folder).
- [ ] Restoration `potionAmount` and both `goldValue`s are placeholders marked as such in
      the report (Revive's 30 is decided, not a placeholder), and
      economy-balance-analyst signs them off (GDD Semester scope item 2: potion values
      belong to the economy table) before Miles merges.
- [ ] `ProfileController.startingInventory` in `Assets/Scenes/SampleScene.unity` holds
      exactly 3 `Potion_Revive` and 3 `Potion_Restoration` (plus whatever 004 leaves
      there). A potion type is only "known" (refillable at the Tavern, upgradable at the
      Alchemist) if it is in the starting inventory (`PlayerLoadout` constructor). Set it
      in the Inspector or via Unity MCP, not by hand-editing YAML.
- [ ] (qa-tester, EditMode) a test loads `Resources/Items`, asserts one Revive and one
      MaxHealthRestore with distinct non-empty `itemId`s, builds a `PlayerLoadout` from
      them, and checks `GetKnownRevivePotionType()` is non-null and `RefillPotions` tops
      Revive up to `GetRefillCapacity`; and asserts the Revive asset's `potionAmount` is 30. Full suite passes via `tools/run-editmode-tests.sh`.
- [ ] (manual, Miles) delete `savegame.json`, start fresh: Potions tab lists 3 Revive
      Draughts and 3 Restoration Potions; Tavern
      shows a refill price or "Fully Stocked"; Alchemist no longer says "No revive potions
      discovered yet"; losing a fight with a Revive carried continues the fight once.

## Context
- Memory: memory/content.md, memory/systems.md (Health; Loot and items)
- May read: `Assets/Scripts/Inventory/ItemData.cs`, `Inventory/PotionEffect.cs`,
  `Inventory/PlayerLoadout.cs`, `Inventory/ProfileController.cs`,
  `Encounters/VillageTavernView.cs`, `Encounters/VillageAlchemistView.cs`,
  `Progression/SaveData.cs` (ItemSnapshot.Restore), GDD sections above
- May write: `Assets/Resources/Items/` (new assets + .meta), `Assets/Scenes/SampleScene.unity`
  (`startingInventory` only)

## Out of scope
- Potion drops. `RewardGenerator.GenerateItem` (line ~183) excludes Potion from random
  drops; the GDD gives potions no drop source (they come from shops), so this stays.
- Selling potions at Market / Wandering Merchant / Alchemist (GDD wants it; not built;
  waits on the economy table).
- Code changes: restoration as % of true max, Revive carry cap, armed Revive toggle,
  one-Revive-per-dungeon-run rule (see Notes).

## Notes
- Refill happens at the **Tavern** (`VillageTavernView`, Revive only); the Alchemist only
  upgrades Revive capacity and quality. Neither refills or sells Restoration potions, so
  starting inventory is a Restoration potion's only source today.
- `itemId` must be set so a save re-links to the asset (`ItemSnapshot.Restore`).
- Existing saves keep their saved (empty) known-potion list; only fresh games pick up the
  starting inventory. No save-shape change.
- Code/GDD conflicts, for Miles, not this task: (a) Restoration restores a flat
  `potionAmount` of HP, GDD says a % of true max (equal only while true max is 100);
  (b) GDD caps Revives carried at 3, but Alchemist capacity upgrades raise it to 8
  (resolved by task 007);
  (c) Revive fires automatically or asks per the `AutoConsumeRevivePotions` setting, GDD
  wants a per-card armed toggle.
- Decided by Miles: a new game starts with 3 Revive and 3 Restoration potions; a Revive
  brings the player back at 30% HP; titles are "Revive Draught" and "Restoration Potion".
- Revive `potionAmount` = 30 means 30%. Until task 007 merges, the code reads it as a flat
  30 HP (the same thing only while max HP is 100); 007 makes it a percentage and makes it
  upgradable at the Alchemist. No open questions remain on this task.
- Approved for merge (Miles, 2026-11-04).
