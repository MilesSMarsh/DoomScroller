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
- Tapping a feed card (or its own action button — labeled for the action it takes:
  "Fight" for Enemy, "Collect" for Treasure, "Enter" for Dungeon/Tavern) commits
  straight into that encounter — no separate preview/confirm screen in between
- Every card also shows a concrete, always-accurate reward expectation (XP + gear
  rarity), not just hand-authored flavor text
- Plain Enemy/Treasure cards resolve in a single step on commit — no difficulty choice
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

### Phase 8 — Simulated Combat & Visual Presentation
**Goal:** Replace the single win/loss roll from Phase 5 with a real simulated fight —
both sides attack automatically at their own attack speed and cast abilities off
cooldown, and the outcome is whoever's HP hits zero first, not a coin flip. Pair this
with a visual rework: sprites animate an attack, gear is visibly present, weapon
abilities/ranged weapons throw a projectile VFX, damage numbers pop up, and the
line-by-line text log goes away entirely. A character stats/level/class page is
explicitly out of scope for this phase.

**Design notes (decisions already locked in, kept here so they aren't re-litigated):**
- The player's real, persistent health pool (the same one that regenerates and gates
  entering encounters) is the live stake during the fight — no separate "combat HP."
  Whatever HP is left after a win carries over and regenerates normally, same as today.
- Enemy combat stats (HP, attack damage, attack speed) are authored explicitly per
  encounter, not derived from the existing difficulty number. That existing number is
  kept, unchanged, solely to drive the feed's win%/close-call% badge — the badge stays
  exactly as it is today, decoupled from how the fight actually resolves; iterate on
  what makes a good difficulty metric later.
- The simulation is an event loop: each combatant's basic attack and each active
  ability is its own timer (basic attacks fire every `1 / attackSpeed` seconds,
  abilities every `cooldownSeconds`, starting off-cooldown so a fight's first ability
  cast happens immediately). Whichever timer is soonest resolves next; that swing has a
  small miss chance and damage variance (not a fully deterministic fight); repeat until
  one side's HP reaches zero.
- Player attack damage/speed come from equipped weapons; defense (damage reduction on
  incoming hits) comes from every other equipped slot (armor/shield/talisman) — a new
  role for the existing per-item power number. Enemies have no defense or abilities of
  their own yet — flat basic attacks only; a natural follow-up once this base sim is
  proven, not built this phase.
- The fight is computed instantly in full, then replayed visually beat-by-beat at a
  fixed pace — presentation pace is intentionally decoupled from the simulated attack
  timing, which could otherwise be uncomfortably fast or slow to actually watch.
- Projectile VFX and attack "wiggle" animations are plain UGUI (an `Image` tweened
  along `anchoredPosition`, a coroutine scale/lunge tween) rather than a real Unity
  `ParticleSystem`/`Animator` — this project's canvas is Screen Space - Overlay with no
  world-space rendering anywhere, so real particle/animator tooling would need
  disproportionate extra plumbing for a still-greybox pass.

**Acceptance criteria:**
- A fight plays out as a sequence of attack beats (basic attacks + ability casts, from
  both sides) with no text-log narration; the enemy's HP and the player's real,
  persistent health both visibly deplete across the sequence, and win/loss is decided
  by whichever hits zero first
- Attacking sprites visibly wiggle/lunge; a weapon ability or a ranged weapon's basic
  attack fires a projectile between the two sprites; every hit pops a damage number
  (or a miss indicator)
- What's equipped is visually present during a fight (not just a hidden stat), and
  changes what's on screen: a ranged weapon's basic attacks throw projectiles where a
  melee weapon's lunge in; an equipped ability casts and hits on its own cooldown
- The feed's win%/close-call% badge is unaffected by this rework — same inputs, same
  behavior as before
- All of the above plays out identically for a plain Enemy/Treasure encounter and for
  each step of a Dungeon run

---

## Deprioritized / Future Scope

Not part of the vertical slice — revisit only after the above phases are proven:

- Crafting tab (equipment/potion creation, stat/ability rerolling)
- Clans and guilds, with forums and trading
- Live multiplayer boss fights
- Full message board system (threading, replies, moderation)
