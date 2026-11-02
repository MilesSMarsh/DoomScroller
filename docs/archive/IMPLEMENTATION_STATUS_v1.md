# Doom Scroller — Implementation Notes for the GDD

Factual summary of what's actually built, compiled from reading the codebase and
`CLAUDE.md`/`GAME_DESIGN.md` directly (not from the design doc's stated intentions,
which in places no longer match the implementation — noted below where they diverge).
Unity 6.x, C#, target platforms iOS/Android (per `CLAUDE.md`; no build pipeline is
configured yet — `CLAUDE.md`'s Build & Test Commands section is still an unfilled
placeholder).

## Name

**Doom Scroller.**

## What the player sees and does, moment to moment

The whole game runs on one full-screen canvas (Screen Space - Overlay, no 3D/world-space
rendering anywhere). A persistent top nav bar shows level/XP/gold and the player's
current HP%. Below it, the player scrolls a vertical feed of cards (`FeedController`,
`FeedCardView`) — an infinite-scroll list backed by a shuffled pool of hand-authored
`EncounterData` assets, each card showing title, art (or a flat color tile if none is
set), a reward-expectation line (XP + rarity odds, always computed live from the card's
own loot table — never static flavor text), a win%/close-call% badge computed from the
player's current loadout, and a type-specific action button (Fight / Collect / Enter /
Gather / Ambush!). Tapping a card's action (or the card itself) commits straight into
that encounter — there is no separate preview/confirm step.

Depending on the card's type, entering it opens one of several full-screen views:
- **Enemy / Ambush** → an animated beat-by-beat combat sequence (lunges, projectiles,
  floating damage numbers, both HP bars draining) ending in a reward chest.
- **Treasure** → an instant reward chest, no fight.
- **Dungeon** → a horizontally-paged multi-room run (see below).
- **Resource** → a timed "deal as much damage as possible to a node that never attacks
  back" minigame, ending in a tiered reward.
- **Tavern** → the Village hub (six sub-screens: Tavern, Inn, Market, Blacksmith,
  Alchemist, Quest Board).
- **Wandering Merchant** → a shop screen with procedurally rolled stock.

A separate Profile screen (reachable from the nav bar at any time, doesn't interrupt the
feed's scroll position) is where the player equips/unequips weapons and armor, views
weapon abilities, assigns attribute points, and manages potions/materials.

## Core loop, as implemented

1. Scroll the feed.
2. Tap into a card.
3. Resolve it — outcome is driven by equipped stats/abilities (a timed simulation, not
   real-time player input); the player never fights directly.
4. Collect XP, gold, and item/material rewards from a reward chest.
5. Return to the feed, or detour into the Profile screen to re-equip, or into the
   Village to shop/craft/upgrade.
6. Repeat, scrolling toward harder cards.

**Divergence from `GAME_DESIGN.md`'s stated loop:** the design doc lists "Respond on
the encounter's message board" as step 5 of the core loop. This is **not actually wired
up that way** — see "Message board" under Stubbed/Missing below. The only live
comment-board UI is generic Tavern chatter, unrelated to any specific encounter's
outcome.

**Divergence the other way:** `GAME_DESIGN.md` lists "Crafting tab (equipment/potion
creation, stat/ability rerolling)" under *Deprioritized / Future Scope*. This has
actually been built — the Blacksmith establishment has working Craft and Upgrade
(Empower/Reroll) tabs, a real recipe-prerequisite tree, and a full material-rarity
system. The design doc is stale here and should be updated to reflect this.

## Win and loss conditions, as implemented

There is no overall "game over" or "victory" state for the game as a whole — it's an
endless progression loop (level, gear, and gold only ever accumulate). Win/loss is
per-encounter:

- **Enemy / Ambush combat** (`CombatSimulator.cs`): an event-loop simulation — every
  equipped weapon's basic attack and every weapon ability is its own timer; the soonest
  timer always resolves next. Each landed swing has an 8% flat miss chance
  (`MissChance = 0.08`) and ±15% damage variance (`DamageVariance = 0.15`). **Win**: the
  enemy's HP reaches 0 first. **Loss**: the player's HP reaches 0 first — the player's
  persistent max HP then drops by 25% of its true maximum (`DefeatPenaltyFraction =
  0.25`), floored at 20% of true max (`FloorFraction = 0.2`, `PlayerHealth.cs`) so a
  losing streak can never fully lock the player out; it regenerates 10 HP every 30
  real-time seconds by default (`ProfileController`'s serialized `regenPerInterval` /
  `regenIntervalSeconds`), credited even while the app is backgrounded. A hard safety
  cap (`MaxBeats = 200`) forces a decision by remaining HP fraction if neither side would
  otherwise die — this is a degenerate-input guard, not a normal outcome. A carried
  Revive potion can continue a fight in progress once per fight.
- **Dungeon**: a sequence of authored rooms (Fight/Rest/Treasure), each independently
  rolling whether it appears. Losing any Fight room ends the run immediately with the
  same defeat penalty as above. Winning works through to a guaranteed final reward room.
  Leaving early also locks the run the same way a loss does.
- **Resource node**: no loss state at all. The player always "succeeds" — total damage
  dealt in a fixed timer against a non-retaliating target determines which reward
  thresholds are met (they can stack).
- **Treasure / Tavern / Wandering Merchant**: no combat, nothing to win or lose.

## AI agents

**None exist in this codebase.** A full search of `Assets/Scripts` and `Assets/Editor`
for networking calls, HTTP/API usage, and any LLM/model-provider references (OpenAI,
Anthropic, Claude, GPT, "agent," "AI," "inference") returned zero matches. Every
system that could superficially resemble "AI-generated content" is deterministic,
local, procedural generation driven by `System.Random`:

- **Enemy combat "behavior"** is not a decision-making agent — an enemy has exactly one
  behavior, a flat basic attack on its own timer (`CombatSimulator`). There is no enemy
  ability use, targeting logic, or adaptive behavior of any kind.
- **The "message board" / NPC chatter** (`Assets/Scripts/Social/MessageBoardGenerator.cs`)
  is not generated text — it's a fixed array of ~6 canned author names and ~16 canned
  one-line comment strings, with `System.Random` picking which canned lines to show and
  in what order. No text is ever composed; nothing is ever sent anywhere.
- **Loot/weapon/item generation** (`WeaponGenerator`, `EquipmentGenerator`,
  `MaterialGenerator`, `RewardGenerator`) rolls stats from hand-authored per-rarity
  tables and ability/passive pools using `System.Random` — procedural generation from
  authored data, not a model of any kind.
- **Shop stock** (`MarketGenerator`, Wandering Merchant) is the same kind of
  `System.Random` roll from an authored catalog.

## Model, API, calls-per-turn, and rate-limit/latency/cost handling

**Not applicable — there is nothing here to report.** No model is called, no API
request is ever made, no network traffic of any kind exists in this project. There is
therefore no rate-limiting, retry/backoff, latency budget, caching, or cost-tracking
code anywhere, because nothing in the game ever calls out to a service that would need
any of that. If this project is meant to incorporate an LLM-driven agent (for the
message board, an NPC, or dynamic content) at some point, that would be new,
ground-up work, not something partially built already.

## Stubbed or missing

- **Per-encounter message board (`GAME_DESIGN.md` Phase 7)**: the generator method for
  it, `MessageBoardGenerator.Generate(EncounterData, bool victory)`, and the view method
  that would show it, `MessageBoardView.Populate(...)`, both exist but **have no live
  caller anywhere in the codebase** — confirmed by searching every call site. The only
  message board a player can actually reach is the Tavern's generic chatter
  (`MessageBoardGenerator.GenerateTavernChatter`, wired into `VillageTavernView`), which
  is never tied to a specific completed encounter. Threading/replies/moderation were
  never started, matching the design doc's own stated scope for this stub.
- **Quest Board**: a Village establishment exists and holds its place in the Village's
  swipeable layout, but has no quest content behind it.
- **Clans/guilds, forums, trading**: not started. No code anywhere references any of
  these concepts.
- **Live multiplayer boss fights**: not started — there is no networking code in the
  project at all, confirmed by the same search used for the AI-agent check above.
- **Build pipeline**: `CLAUDE.md`'s Build & Test Commands section is unfilled; nothing
  in this project has been confirmed to build/run outside the Unity Editor.
