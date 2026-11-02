# Systems

One entry per system: what it does now, how it works, key files, and where it differs
from `docs/gdd/GDD.md` ("Gap"). Describes the current game, not a history. The Producer
rewrites an entry in place when a feature merges.

All paths are under `Assets/Scripts/` unless noted. Single scene: `Assets/Scenes/SampleScene.unity`.

## Combat
- Event-loop sim: each weapon basic attack and each ability is its own timer; soonest
  resolves next. 8% miss, ±15% variance. `MaxBeats = 200` guard decides by HP fraction.
  Takes an injected `System.Random`. Fight is computed in full, then replayed as beats
  (lunges, UGUI projectiles, damage numbers). Revive potion can continue a fight once.
- Enemies: one flat basic attack, no defense or abilities.
- Win odds: `WinOddsEstimator.Estimate` runs 64 seeded practice fights
  (`DefaultRunCount`) via `CombatResolver.CreateSimulator`, auto-cast, no revive, and
  returns the win fraction plus a `WinOddsTier`: Likely >= 0.70, Risky >= 0.35, else
  Long shot. Non-fight types (Treasure, Tavern, Resource, Landing, WanderingMerchant,
  and Dungeon for now) skip the sim and return 1 / Likely. Cards are to show only the
  tier word, no %.
- Key files: `Combat/CombatSimulator.cs`, `Combat/CombatResolver.cs`,
  `Combat/WinOddsEstimator.cs`, `Combat/WinOddsTier.cs`, `Combat/CombatBeat.cs`,
  `Combat/CombatPlaybackView.cs`, `Encounters/CombatResolutionView.cs`.
- Gap: `WinOddsEstimator` isn't wired into `FeedController`/`FeedCardView`; the feed
  badge still shows win%/close-call% from the DPS heuristic
  (`CombatResolver.PredictWinChance`/`PredictCloseCallChance`). Simulator
  `using UnityEngine` — GDD needs a Unity-free shared library. Seed isn't drawn/saved
  at tap-in. No 2x speed or skip. Revive is carried, not armed via a card toggle.

## Health, loss and regen
- Persistent max HP with a 20% floor (`FloorFraction = 0.2`). Loss drops max HP by 25%
  of true max (`DefeatPenaltyFraction = 0.25`). Regen 10 HP / 30 s by default
  (serialized on `ProfileController`), credited while backgrounded using device time.
- Key files: `Progression/PlayerHealth.cs`, `Inventory/ProfileController.cs`.
- Gap: GDD wants 10% loss, 1% of true max per 36 s, Inn 2×/3× offline multiplier,
  every fight starting at full current max HP, rewards scaling with max HP, server time.

## Feed
- Infinite vertical feed from a shuffled pool of `EncounterData` loaded from
  `Resources/Encounters`. Card shows title, art, live reward line from its loot table,
  win%/close-call badge, typed action button. A tap commits — no hold.
- Key files: `Feed/FeedController.cs`, `Feed/FeedCardView.cs`, `Feed/FeedPager.cs`,
  `Input/TouchGestureService.cs`.
- Gap: no regions, difficulty ranges, swipe pressure, streak penalty, swipe-back
  history, hold-to-commit, Revive toggle, first-session scripted cards, or Ambush rules
  tied to the floor.

## Encounters and dungeons
- Types (`Feed/EncounterType.cs`): Treasure, Enemy, Dungeon, Tavern (Village), Resource,
  Landing, WanderingMerchant, Ambush. `Encounters/EncounterFlowController.cs` routes a
  committed card to its view.
- Dungeon: authored rooms (Fight/Rest/Treasure), each rolling whether it appears; horizontal
  paging; loss or leaving locks the run. `Encounters/DungeonGenerator.cs`, `DungeonView.cs`,
  `DungeonStepPageView.cs`.
- Resource node: timed damage-dealing minigame, stacked reward thresholds.
  `Encounters/ResourceEncounterView.cs`.
- Gap: dungeon run state isn't saved per room; no 3-minute background rule; equipment
  lock during a run should be verified. No Boss or Events types.

## Loot and items
- Every drop has a rarity and rolled stats/abilities. Weapons roll from authored
  `WeaponDefinition` pools; shops must use `GenerateFromDefinition`/
  `GenerateFromRandomDefinition`, never archetype `Generate` (used only for drops).
- Key files: `Progression/RewardGenerator.cs`, `WeaponGenerator.cs`, `EquipmentGenerator.cs`,
  `MaterialGenerator.cs`, `Encounters/RewardChestView.cs`.
- Gap: drop rates don't scale with max HP; no economy table.

## Profile and progression
- Profile screen over the feed (keeps scroll position): equip slots, abilities,
  attribute points, potions, materials. XP levels grant attribute points.
- Key files: `Inventory/PlayerLoadout.cs`, `Inventory/ProfileView.cs`,
  `Inventory/AttributesPanelView.cs`, `Progression/PlayerProgression.cs`,
  `Progression/PlayerAttributes.cs`, `UI/NavBarController.cs`.

## Crafting
- Blacksmith: craft from `RecipeDefinition`s with material prerequisites (recipe tree);
  Empower and Reroll via `WeaponUpgradeConfig`. Material rarity matches crafted rarity.
- Key files: `Encounters/VillageBlacksmithView.cs`, `Progression/WeaponUpgrader.cs`,
  `Progression/RecipeCatalog.cs`, `Inventory/RecipeDefinition.cs`.

## Village and shops
- Swipeable hub: Tavern, Inn, Market, Blacksmith, Alchemist, Quest Board. Wandering
  Merchant is a feed card with rolled stock.
- Key files: `Encounters/VillageView.cs` and `Village*View.cs`,
  `Encounters/WanderingMerchantView.cs`, `Progression/MarketGenerator.cs`,
  `Progression/WanderingMerchantGenerator.cs`, `Progression/PlayerWallet.cs`.
- Gap: Quest Board is an empty shell. Buildings don't unlock progressively. Inn doesn't
  apply an offline regen multiplier. Prices aren't driven by an economy table.

## Social
- Tavern shows canned chatter (`MessageBoardGenerator.GenerateTavernChatter`). A
  per-encounter board (`Generate` + `MessageBoardView.Populate`) exists but has no caller.
- Key files: `Social/MessageBoardGenerator.cs`, `Social/MessageBoardView.cs`.
- Gap: GDD removes all canned/generated messages; real player chat with filter, report,
  block and moderation queue; separate authored keeper dialogue.

## Save and persistence
- Single local JSON save at `Application.persistentDataPath/savegame.json`
  (`JsonUtility`). Items re-link to their definitions by id via `Resources.LoadAll`.
- Key files: `Progression/SaveSystem.cs`, `Progression/SaveData.cs`.
- Gap: GDD is online-only — server owns profile, seeds, fight results, regen clock,
  dungeon state, daily reset. No network code exists. Backend not chosen yet.

## Build and test
- EditMode tests in `Assets/Tests/EditMode/` (asmdef `DoomScroller.Tests.EditMode`),
  run headless with `tools/run-editmode-tests.sh`. No PlayMode tests, no build scripts,
  no batch-mode simulation runner for the Balance Analysts yet.
