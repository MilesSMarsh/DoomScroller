# Systems

One entry per system: what it does now, how it works, key files, and where it differs
from `docs/gdd/GDD.md` ("Gap"). Describes the current game, not a history. The Producer
rewrites an entry in place when a feature merges.

All paths are under `Assets/Scripts/` unless noted. Single scene: `Assets/Scenes/SampleScene.unity`.

## Combat
- Shared library `CombatCore/` (asmdef `DoomScroller.CombatCore`, no Unity references,
  C# 9, .NET Standard 2.1-safe so the server can compile it). `CombatSimulator.Resolve(
  FightInput)` resolves a whole fight from plain data (player side, enemy side, `int`
  seed, armed Revive) into a `FightResult` (outcome, ordered `CombatBeat[]`, Revive
  fired + beat index, final HP); both round-trip through `JsonUtility`.
- Event-loop sim: each weapon basic attack and each ability is its own timer; soonest
  resolves next. 8% miss, ±15% variance, banker's rounding. `MaxBeats = 200` guard
  decides by HP fraction. Abilities always auto-cast; no player input mid-fight.
- Revive: fires automatically once per fight on death, restoring `revivePercent` of the
  fight's start HP (rounded up, min 1). Armed = a Revive is carried and
  `GameSettings.AutoConsumeRevivePotions` is on (default off; toggle in the Potions tab).
- `Combat/FightInputBuilder` builds `FightInput` from `PlayerLoadout` +
  `PlayerAttributes` + `EncounterData`/`DungeonStep`/resource node + start HP; used by
  `CombatResolver`, `WinOddsEstimator`, dungeon and resource-node views.
- Playback: `CombatResolver.ResolveEncounter`/`ResolveStep` resolve the fight up front
  on the device (seed from a device `System.Random`); `CombatPlaybackView.Play` replays
  the finished `FightResult` beat by beat (lunges, UGUI projectiles, damage numbers,
  cooldown bars from beat timing). `FightReplay` (pure) maps a result to the HP
  timeline, Revive HP and close-call flag; "REVIVED!" plays at the fired beat and the
  carried Revive is consumed only if the result says it fired. Resource nodes still run
  `PlayTimedWindow` off a `CombatSimulator.RunForDuration`.
- Enemies: one flat basic attack plus per-damage-type multipliers
  (`damageTypeModifiers`); no armour or abilities.
- Win odds: `WinOddsEstimator.Estimate` runs 64 seeded practice fights
  (`DefaultRunCount`), no revive, and returns the win fraction plus a `WinOddsTier`:
  Likely >= 0.70, Risky >= 0.40, else Long shot. Non-fight types (Treasure, Tavern,
  Resource, Landing, WanderingMerchant, and Dungeon for now) skip the sim and return
  1 / Likely.
- Fight screen pages sideways to the reward page only after a win; during a fight, after
  a loss, and on Treasure (pinned to the reward page) the live drag is clamped to
  reachable pages (`CombatResolutionView.ClampDragX`).
- Key files: `CombatCore/CombatSimulator.cs`, `CombatCore/FightInput.cs`,
  `CombatCore/FightResult.cs`, `CombatCore/CombatBeat.cs`, `CombatCore/CombatStats.cs`,
  `Combat/FightInputBuilder.cs`, `Combat/CombatResolver.cs`,
  `Combat/WinOddsEstimator.cs`, `Combat/CombatPlaybackView.cs`, `Combat/FightReplay.cs`,
  `Encounters/CombatResolutionView.cs`, `Encounters/DungeonStepPageView.cs`.
- Gap: fights resolve on the device, not the server (tasks 016/017); seed isn't
  drawn/saved at tap-in, so quitting mid-replay doesn't replay the same fight. No 2x speed or skip. Revive is armed by a global setting, not a
  per-card toggle.

## Health, loss and regen
- Persistent max HP with a 20% floor (`FloorFraction = 0.2`); `IsAtFloor` is true when
  current max <= floor (the feed uses it to block Ambushes). Loss drops max HP by 25%
  of true max (`DefeatPenaltyFraction = 0.25`). Regen 10 HP / 30 s by default
  (serialized on `ProfileController`), credited while backgrounded using device time.
- Key files: `Progression/PlayerHealth.cs`, `Inventory/ProfileController.cs`.
- Potions: two `ItemData` types in `Resources/Items/` — Revive Draught
  (`potion_revive`, `potionAmount` 30) and Restoration Potion (`potion_restoration`,
  restores a flat 25 HP). A new game starts with 3 of each via
  `ProfileController.startingInventory` (SampleScene); a type is "known" (Tavern refills
  Revive, Alchemist upgrades it) only if it's in the starting inventory. Loaded saves keep
  their saved known-potion list. Starting inventory is the only Restoration source.
- Gap: Restoration is a flat `potionAmount` HP; GDD wants a % of true max. Restoration
  25 HP and potion gold values (Revive 40, Restoration 20) are placeholders without
  economy-balance-analyst sign-off. No shop sells potions yet.
- Gap: GDD wants 10% loss, 1% of true max per 36 s, Inn 2×/3× offline multiplier,
  every fight starting at full current max HP, rewards scaling with max HP, server time.
- Revive: restores a percent of the fight's start HP (see Combat):
  the asset's `potionAmount` (30) is the base percent, +10 per Alchemist level
  (`RevivePercentPerLevel`), 7 levels, capped at 100. Carry cap is 3 for every potion
  type (`GetRefillCapacity`); Tavern refills to 3. Potions tab shows "Revives at N% HP".
  Save stores the level in `PotionUpgradeEntry.qualityLevel` (old flat-HP saves not
  migrated).

## Feed
- Infinite vertical feed from a shuffled pool of `EncounterData` loaded from
  `Resources/Encounters`. Card shows title, art, live reward line from its loot table,
  odds badge, typed action button. A tap commits — no hold.
- Odds badge: `WinOddsEstimator` tier word only (Likely / Risky / Long shot), placeholder
  per-tier colours from static `FeedBadge` (in `FeedCardView.cs`). Hidden on non-fight
  cards, Dungeon included. Start HP = `Health.CurrentMaxHealth`. Odds refresh on loadout
  change only, not on HP changes (defeat penalty or regen).
- Draw rules: `FeedDrawRules.IsDrawable(type, playerAtFloor)` blocks new Ambush draws
  while `Health.IsAtFloor`. `DrawNext` skips undrawable entries but leaves them in the
  shuffle bag (each drawable entry still once per pass); refills when only undrawable
  entries remain; if none are drawable it logs a warning and draws one anyway. Ambushes
  already in the live window are not removed when the player hits the floor.
- Key files: `Feed/FeedController.cs`, `Feed/FeedDrawRules.cs`, `Feed/FeedCardView.cs`,
  `Feed/FeedPager.cs`, `Input/TouchGestureService.cs`.
- Gap: no regions, difficulty ranges, swipe pressure, streak penalty, swipe-back
  history, hold-to-commit, Revive toggle, or first-session scripted cards. Badge colours
  still need ui-stylist. Device performance of the
  badge sims (64 per fight card) is unchecked.

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
  `GenerateFromRandomDefinition`, never archetype `Generate` (only the fallback when
  no definitions load). Potions never drop.
- Loot tables with empty pools (all `Dungeon_*`, `Enemy_RogueSlime`) and
  `RewardGenerator.GenerateItem` (Market, Wandering Merchant) draw named items from the
  authored catalogs: `WeaponGenerator.GenerateFromRandomDefinition` and
  `EquipmentGenerator.GenerateFromRandomDefinition` (by category, from
  `Resources/EquipmentDefinitions`). Generic "{rarity} {slot}" items only appear when a
  category has no definition.
- Weapons are generated with `slotType = RightHand`.
- Key files: `Progression/RewardGenerator.cs`, `Progression/WeaponGenerator.cs`,
  `Progression/EquipmentGenerator.cs`, `Progression/MaterialGenerator.cs`,
  `Encounters/RewardChestView.cs`.
- Gap: drop rates don't scale with max HP; no economy table.

## Profile and progression
- Profile screen over the feed (keeps scroll position): equip slots, abilities,
  attribute points, potions, materials. XP levels grant attribute points.
- Key files: `Inventory/PlayerLoadout.cs`, `Inventory/ProfileView.cs`,
  `Inventory/AttributesPanelView.cs`, `Progression/PlayerProgression.cs`,
  `Progression/PlayerAttributes.cs`, `Progression/StarterWeapon.cs`,
  `Inventory/ProfileController.cs`, `UI/NavBarController.cs`.
- New game (`StartFresh` only) adds and equips a Plain Hallow Wood Sword rolled by
  `Progression/StarterWeapon.Create` → `WeaponGenerator.GenerateFromDefinition`, from
  `ProfileController.starterWeaponDefinition` (scene: `WeaponDefinitions/WoodenSword`).
  It lands in `RightHand`. Unassigned → warning, starts unarmed. Loaded saves keep their
  loadout (no migration for saves without a weapon).
- Items only equip into slots their category allows (`ItemCategoryInfo.SlotsFor`):
  `Equip(item)` falls back to the first allowed slot when `item.slotType` isn't allowed.
  On load, an item saved in a disallowed slot (e.g. a weapon in Head) moves to its first
  free allowed slot, else is left unequipped.

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
- Alchemist: one Revive upgrade track (+10 percentage points per level, "Maxed" at 100%).
  Costs (15 gold + 10/level, 2 materials + 1/level) and the Tavern per-level refill
  surcharge are placeholders.
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
  dungeon state, daily reset. The client signs in but syncs nothing yet (server move:
  tasks 008–018).
- Gap: client-authoritative economy (cheatable). After the server move the client still
  rolls rewards and runs shops and crafting, and uploads the whole save; only fights,
  seeds, HP loss, Revive use and current max HP (incl. regen) are server-owned.

## Server move (decisions)
- Server: self-hosted ASP.NET Core (.NET 10) in a container under `Server/`, compiling the
  shared Unity-free CombatCore source. Client code in `Network/` (`DoomScroller.Network`).
- Data: managed Postgres; profile = today's `SaveData` as one JSONB document per player
  (with revision) plus a `fight_records` table. Docker Postgres for local dev. Proper
  tables come after the economy table.
- Identity: anonymous sign-in by install id. Game Center / Google Play Games linking is a
  later task before soft launch.
- Hosting: chosen after the local slice works (leaning Azure Container Apps); the slice
  deploys nothing.
- Combat: no manual ability casting, no mid-fight Revive prompt. Abilities auto-cast; a
  carried Revive auto-fires when `AutoConsumeRevivePotions` is on, until the per-card
  armed toggle exists.
- Authority: server owns fights, seeds, HP loss, Revive use and the regen clock; client
  owns rewards, shops and crafting (see Gap above).
- Built (server): `GET /health` (version, UTC time, DB reachable); `POST /auth/anonymous`
  (install id -> player id in Postgres `players`, nullable platform columns reserved for
  later linking) issuing a JWT signed with `JWT_SIGNING_KEY`; every other endpoint needs
  the token (401), e.g. `GET /auth/me`. Env-var config, SQL migrations in
  `Server/src/DoomScroller.Server/Migrations/`, xUnit tests; `tools/run-server-dev.sh`
  (port 8088; rebuild the image after server changes), `tools/run-server-tests.sh`.
- Built (client): `Network/ServerSession` signs in at startup (no scene object) and
  exposes `ServerSession.Client` (`IServerClient`). `ServerClient` keeps install id,
  token and player id in PlayerPrefs, re-signs in once on a 401, then fails. Base URL
  and timeout from `ServerSettings` (`Resources/ServerSettings`; none authored, so it
  falls back to http://localhost:8088, 10 s). Key files: `Network/ServerClient.cs`,
  `Network/IServerClient.cs`, `Network/UnityWebRequestTransport.cs`,
  `Server/src/DoomScroller.Server/Auth.cs`.
- Gap: no game endpoints yet; sign-in failure only logs a warning (no offline screen
  until task 014).

## Build and test
- EditMode tests in `Assets/Tests/EditMode/` (asmdef `DoomScroller.Tests.EditMode`) and
  Unity-free sim tests in `Assets/Tests/CombatCore/` (`DoomScroller.CombatCore.Tests`),
  run headless with `tools/run-editmode-tests.sh`. No PlayMode tests, no build scripts,
  no batch-mode simulation runner for the Balance Analysts yet.
- `ResourcesNamingTests` guards asset names, GUIDs, identity fields and dangling GUID
  references in authored content (`Resources/**/*.asset`, scenes, prefabs).
- Gap: `Assets/Settings/Renderer2D.asset` holds 7 dangling URP probe-volume debug GUIDs
  (pre-existing, outside the naming test's scope); needs a project-settings clean-up task.
