# Doom Scroller — Game Design Document

## Concept

A mobile MMORPG (iOS + Android) that replicates the feel of scrolling a social feed
(Instagram/TikTok-style). Each "post" in the feed is a combat encounter. Players swipe
through the feed to find and enter encounters, then manage their character through a
social-media-style profile page (inventory / equipment / skill tree).

## Core Loop

1. Scroll the feed
2. Swipe into an encounter (harder encounters = better rewards, accessible via swiping sideways within a post)
3. Resolve the encounter (combat is minimally interactive — outcome driven by loadout, not real-time skill)
4. Collect XP + equipment/material rewards
5. Respond on the encounter's message board (social layer)
6. Return to profile page to re-equip / upgrade
7. Repeat, seeking harder encounters

## Full Feature Set (long-term vision)

- Scrollable feed of encounters (treasure / enemy / boss)
- Profile page as inventory, equipment, and skill tree hub
- Live multiplayer boss fights triggered from the feed
- Crafting tab: create equipment/potions, reroll stats/abilities on items
- Clans/guilds with forums for chat and trading
- Message boards per encounter/instance

## Vertical Slice Scope

The vertical slice is the target for the first playable build. It intentionally cuts
scope down to prove the core loop before building out the social/multiplayer layers:

- A scrollable feed with 2–3 encounters: one treasure encounter, one enemy encounter,
  one harder enemy encounter
- An inventory/equipment screen to equip armor, weapons, and manage weapon abilities
- A working gameplay loop: enter encounter → resolve → get reward → upgrade
  character → repeat

Everything outside this list (crafting, clans/guilds, live multiplayer bosses, full
message boards) is explicitly **out of scope** until the vertical slice loop is proven
fun and functional.

---

## Implementation Phases

> Note: this assumes Unity as the target engine. Update this section if that changes.
> Work through these phases sequentially — each should be functionally complete and
> testable before moving to the next.

### Phase 1 — Mobile Project Setup
**Goal:** A Unity project correctly targeting iOS and Android with a basic UI shell.
**Acceptance criteria:**
- Project builds and deploys to both an Android device/emulator and iOS
  simulator/device
- Basic touch input (tap, swipe) registers correctly on-device
- A placeholder full-screen UI canvas is in place, sized for phone aspect ratios

### Phase 2 — Scrollable Feed
**Goal:** A vertically scrolling feed UI that mimics a social media feed.
**Acceptance criteria:**
- Feed scrolls smoothly with touch/swipe input
- Feed contains distinct "post" cards, each representing one encounter
- Cards are populated from data (not hardcoded per-card), so new encounters can be
  added without new UI code
- At least 3 cards render correctly: 1 treasure, 2 enemy (one harder than the other)

### Phase 3 — Encounter Entry & Dungeons
**Goal:** Entering an encounter from the feed. Plain encounters resolve in one step;
Dungeon-type encounters are a distinct, multi-step "dungeon door" card.
**Acceptance criteria:**
- Tapping/entering a feed card transitions into that encounter
- Plain Enemy/Treasure cards preview the entrance, then resolve in a single step on
  commit — no difficulty choice
- A Dungeon card commits into a horizontally-paged run: winning a step and swiping
  right presses one page deeper into a new (harder) enemy, ending in a guaranteed
  treasure room; swiping left revisits already-conquered pages without re-fighting
  them; a downward swipe prompts to leave
- Leaving a dungeon early, dying at any depth, and clearing the treasure room all lock
  the feed card so the run can't be freely re-entered

### Phase 4 — Character Profile Screen (Inventory/Equipment)
**Goal:** A "bio page"-styled screen for managing the character.
**Acceptance criteria:**
- Screen displays currently equipped armor, weapon, and other equipment slots
- Player can equip/unequip items from their inventory
- Weapon abilities tied to equipped weapon are visible and manageable from this screen
- Screen is reachable from the feed and doesn't interrupt the feed's scroll state

### Phase 5 — Combat Resolution (Minimal Interactivity)
**Goal:** Encounters resolve based on loadout rather than real-time player skill.
**Acceptance criteria:**
- Combat outcome is calculated from equipped stats/abilities, not twitch input
- Player sees a clear resolution sequence/summary (not just an instant win/loss)
- Losing and winning are both handled gracefully with appropriate feedback

### Phase 6 — Rewards & Progression Loop
**Goal:** Close the loop — reward, upgrade, repeat.
**Acceptance criteria:**
- Winning an encounter grants XP and equipment/material rewards
- XP contributes to a visible character progression (level or equivalent)
- Rewarded equipment can immediately be equipped from Phase 4's screen
- Player can return to the feed and repeat the loop without friction

### Phase 7 — Message Board Stub (Vertical Slice Only)
**Goal:** A minimal version of the social response layer, enough to validate the
concept — not the full social system.
**Acceptance criteria:**
- Completed encounters show a simple message board / comment list
- Player can post a response after completing an encounter
- Full threading, replies, and social features are explicitly deferred

---

## Deprioritized / Future Scope

Not part of the vertical slice — revisit only after the above phases are proven:

- Crafting tab (equipment/potion creation, stat/ability rerolling)
- Clans and guilds, with forums and trading
- Live multiplayer boss fights
- Full message board system (threading, replies, moderation)
