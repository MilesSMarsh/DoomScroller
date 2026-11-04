# Doom Scroller — Game Design Document

Oct 20, 2026 · Miles

> Converted from `Doom_Scroller_GDD_v2.pdf` (same folder). This Markdown file is the
> working copy agents read; Miles edits it as the design changes. Section anchors are
> stable for task specs to reference (e.g. `docs/gdd/GDD.md#combat`).

## Executive Summary

**Doom Scroller** is a mobile RPG for iPhone and Android that plays like a social media
feed: every post is an encounter, and you scroll, tap in, and watch your character fight
a simulated battle decided by the gear you equipped.

**Core loop:** scroll the feed → tap into an encounter (or swipe past it) → watch the
simulated fight → open the reward chest → re-equip or upgrade in your Profile or the
Village → scroll on toward harder cards.

**Win and loss:** each encounter is won when the enemy's HP hits zero first and lost
when yours does. Every fight starts at full HP, equal to your current max HP. A loss
lowers max HP by 10% of its true value (never below 20%), rewards shrink in line with
max HP, and it recovers over time, faster when resting at the Inn, or with potions, so a
losing streak slows you down but never locks you out. There is no final "game over";
progress (level, gear, gold) only accumulates.

**Genre and session:** a minimally interactive RPG with asynchronous multiplayer, built
for short sessions: most cards resolve in seconds. Strategy happens in the loadout, not
in reflexes.

**Tone and the feed:** a sincere fantasy adventure first, with satire of feed culture
hidden underneath for players who look for it. The satire's point is that mindless
scrolling is costly: the world punishes skimming and rewards attention, mocking
autopilot scrolling rather than engagement itself. The target audience is idle-RPG
players, adults 18–35 who play in short mobile sessions and prefer loot and
build-crafting to twitch combat. The feed isn't an object in the world; it is how the
player travels through it, and each card is the next thing the hero meets.

**Business model:** free to play, soft-launched on the App Store and Google Play in a
few countries by the end of the semester. The store sells convenience only. Potions and
gold are never sold for real money. Faster Inn rest is the one purchase that touches
recovery, so it stays modest: it raises Inn recovery from 2× to 3×, saving at most 8
minutes on a full recovery from the floor.

**Status:** a playable Unity 6 prototype exists, built with Claude Code. The feed, six
encounter types, simulated combat with animated replay, dungeons, the Village (Tavern,
Inn, Market, Blacksmith, Alchemist), crafting, and equipment are working. Regions with
solo bosses, live world and raid bosses, Tavern player chat and daily missions are
planned.

**Development approach:** for the full game, a flexible team of Claude-based
development agents will build, author, balance, and test the game, with Miles as
creative director. The shipped game makes no AI calls at runtime.

## Game Mechanics

The player never fights directly: every choice is made before the fight (which card to
enter, what to equip), and the fight itself is a short animated replay of a simulation.

### What the player sees

One full-screen feed. A top bar always shows level, XP, gold and max HP (as a % of true
max). Each card shows:

- Title and art for the encounter
- A reward line (XP and gear-rarity odds), calculated live from that card's loot table
- A win-odds badge showing **Likely**, **Risky** or **Long shot**, calculated from the
  player's current loadout
- One action button named for what it does: Fight, Collect, Enter, Gather, or Ambush!
  It's pressed and held to commit, and a Revive toggle beside it arms a potion for that
  fight and updates the badge instantly

### Second to second

1. Swipe up to the next card. The last two cards stay reachable by swiping back:
   skipped cards can still be entered, played cards are locked, and an Ambush can't be
   escaped by swiping back.
2. Read the badge and reward line, then decide.
3. Press and hold the button to commit (a quick tap or a swipe can't trigger it), or
   swipe past to skip. Ambush cards cannot be skipped.
4. Watch the result, collect the chest, and land back on the feed where you left it.

### First session

A short story opening introduces who the player is and what the feed is, written from
the narrative brief. Then the feed starts safe:

- The first ~10 cards are hand-picked and easy, with no Ambushes and no max HP loss.
- One early card teaches the win% badge and the hold-to-commit button. Another teaches
  swipe pressure: skimming past cards makes reward cards rarer and Ambushes likelier,
  and fighting a real enemy resets it. Around card 10, a scripted, low-stakes first loss
  costs a little max HP, opens the Inn and explains recovery, followed by a gear swap
  that visibly moves a card's badge tier.
- Village buildings unlock as they become useful: the Blacksmith on the first crafting
  material, the Inn on the first loss.
- **Open question:** Miles writes the story opening.

### Minute to minute

The player decides what to chase right now: which cards to enter, which to skip, and
which gear to grind for and craft.

- Scan the feed for cards whose reward line drops the materials or gear rarity you need.
- Pick Resource nodes and dungeons to farm crafting materials, and fights for XP and
  gold.
- Weigh each card's win% badge against your current max HP, since a loss lowers it
  further and lower max HP means smaller rewards.
- Spend the haul at the Blacksmith (craft, Empower, Reroll), Market or Alchemist.

### Hour to hour

The player shapes a build: what kind of character am I making, and which upgrades make
it win more?

- Commit to a build direction, such as fast basic attacks, ranged projectiles, or
  ability-cooldown spam, and gear around it.
- Spend attribute points and pick weapon abilities that support that build.
- Upgrade and reroll key items until the badge shows harder cards as winnable.
- Manage the max HP debuff: after a loss, choose between waiting for regeneration,
  resting at the Inn, using restoration potions, taking safer cards, or pushing on at
  reduced max HP. Rewards shrink in line with max HP.

### Day to day

The player returns for daily missions and long-term power.

- **Daily missions** on the Quest Board (planned) give bonus rewards for goals such as
  clearing a dungeon or defeating a number of enemies.
- Each session builds toward the next region, unlocked by beating that region's solo
  boss.
- Max HP regenerates while the app is closed, so a day's losses are recovered by the
  next session.

### Feed generation

The feed is built from regions and rewards engaging with the cards in front of you over
skimming past them.

- **Regions:** the feed is split into regions, each with a fixed difficulty range. A
  player unlocks the next region with an item dropped by its solo boss, which drops only
  when the boss is beaten above 50% max HP. Card difficulty comes from the region, not
  the player's gear, so upgrades make a region feel visibly easier, and re-equipping
  can't change the difficulty of cards already in the feed.
- **Card mix:** mostly Combat, a minority of Treasure and Resource nodes, occasional
  Merchants and Dungeons, and rare Ambushes. A few stretch cards sit at the top of a
  region's range and show better rarity odds. The Combat Balance Analyst tunes the exact
  weights.
- **Swiping has a cost:** each card swiped past makes no-risk reward cards (Treasure,
  Resource nodes) rarer, and swiping quickly raises the chance of an Ambush. Players who
  engage with the feed find more rewards; players who skim it find fewer rewards and
  more forced fights.
- **No farming streaks:** no-risk cards pay less when played back to back, and skipping
  a card in between doesn't reset the streak.
- **Ambush:** difficulty follows the current region, so being debuffed never makes
  forced fights easier. Ambushes don't appear while a player is at the 20% floor.
- **Resets and return trips:** playing a combat card at or above the region's base
  difficulty resets swipe pressure, so tapping a trivial fight can't clear it. Players
  can return to any unlocked region freely, with normal rewards. Each new region's
  resource rewards are tuned to be more efficient to farm than earlier regions', so an
  overpowered character returning to an old region is never the best way to gather
  materials. The Economy Balance Analyst owns this curve.

### Encounter types

| Encounter | What the player does | Win / loss | Status |
|---|---|---|---|
| Combat (Enemy) | Taps Fight, watches the simulated battle | Win: chest. Loss: HP penalty | Built |
| Ambush | Forced fight, cannot be skipped | Same as Combat | Built |
| Treasure | Taps Collect, opens a chest | No risk | Built |
| Resource node | Gear hits a node that never fights back for a fixed time | Damage dealt sets reward tiers, which stack; no loss | Built |
| Dungeon | Pages sideways through rooms (Fight, Rest, Treasure); Rest rooms grant a buff for the rest of the run | Clear every fight to reach a guaranteed final reward; a loss, leaving early or closing the app ends and locks the run, keeping loot from cleared rooms | Built |
| Village | Visits Tavern, Inn, Market, Blacksmith, Alchemist, Quest Board | No risk | Built (Quest Board to host daily missions) |
| Wandering Merchant | Buys from randomly rolled stock | No risk | Built |
| Boss | Fights a solo, raid or world boss (see [Boss types](#boss-types)) | Win: loot by contribution tier. Loss: a normal defeat for that player only | Planned |
| Events | Other one-off cards | To be designed | Planned |

### Combat

Every weapon's basic attack and every ability runs on its own timer: attacks fire at the
weapon's attack speed, abilities fire whenever their cooldown is ready (or on another
trigger). Whichever timer is due next resolves, with an 8% chance to miss and ±15%
damage variance, until one side reaches 0 HP.

The fight is decided the moment the player taps in, using a seed drawn at that instant,
then shown as a beat-by-beat replay. There is no retreat. Before committing, the player
can arm a Revive potion with the toggle on the card; if their HP hits zero, an armed
Revive fires automatically and the fight carries on, so every choice is still made
before the fight. Fights can be sped up to 2× or skipped to the result. While it plays,
sprites lunge, ranged weapons and abilities throw projectiles, damage numbers pop, and
both HP bars drain. What you equip changes what you see; a bow shoots where a sword
lunges. Enemies currently have one flat basic attack.

Damage never carries over between fights: every fight starts at full HP, equal to the
player's current max HP. Max HP is the persistent stake, and it doubles as the game's
energy system: losses spend it, time and rest refill it, and because rewards scale with
it, a player at low max HP earns less until they recover. It paces sessions without a
separate energy bar. The target is an active session of 20–30 minutes before a typical
player runs low; the Combat Balance Analyst tunes loss rates toward it.

- **Loss:** max HP drops by 10% of its true value, additively, to a floor of 20%
  (100% → 90% → 80% … → 20%, eight losses). Small losses keep players willing to take
  fights.
- **Rewards scale with max HP:** gold, XP and gear drop rates all scale with current max
  HP. At 90% max HP a player gets 90% of normal rewards and drop rates; at 50%, half; at
  the 20% floor, a fifth. Gear can still drop at the floor, but too rarely to be worth
  farming. No Ambushes appear while a player is at the floor, and a loss at the floor
  costs nothing beyond the reduced rewards.
- **Regeneration:** max HP recovers 1% of true max every 36 seconds, so each loss heals
  in 6 minutes at any level. It keeps recovering while the app is closed, measured
  against server time. A player who enters the Inn before closing the app recovers twice
  as fast for as long as the app stays closed, or three times as fast with the paid
  convenience upgrade. Full recovery from the floor takes 48 minutes normally, 24 at the
  Inn and 16 with the upgrade.
- **Restoration potions:** used outside fights, each restores a set percentage of true
  max HP.
- **Revive potions:** armed before a fight and fired automatically. Limits: one per
  fight, one per dungeon run, at most three carried. A Revive brings the player back at
  30% HP; Alchemist upgrades raise that percentage.

### Progression

- **Profile screen** (reachable any time without losing your feed position; equipment
  is locked during a dungeon run): equip weapons and armor, view weapon abilities, spend
  attribute points, manage potions and materials.
- **Loot:** every drop has a rarity and randomly rolled stats and abilities.
- **Blacksmith:** craft from recipes with material prerequisites, then Empower or Reroll
  items.
- **Market, Wandering Merchant, Alchemist:** spend gold on gear and potions. Before any
  shop work, an economy table owned by the Economy Balance Analyst sets prices, drop
  rates, stock rules and pacing targets, and gives each shop a distinct role.
- **XP:** levels up the character and grants attribute points.

### Multiplayer

Asynchronous multiplayer is a core feature, not a stretch goal. Players never need to be
online at the same moment, except for live world and raid boss fights.

- **Boss fights:** three types (solo, raid and world), described under
  [Boss types](#boss-types) below.
- **Tavern message boards:** each village Tavern hosts a message board for
  player-to-player chat. It contains only messages written by real players; there are no
  pre-written or generated messages. The canned chatter in the prototype is a
  placeholder and will be removed. As a store release, chat includes a word filter,
  reporting and blocking, and reported messages go to a moderation queue that Miles
  reviews. Village keepers get their own authored dialogue, separate from the player
  board.
- **PvP arena:** unlocked in a later region and found as a Village encounter. A player
  registers their character, and it asynchronously defends against other players who
  enter the arena; entering the arena means fighting other players' registered
  characters the same way. Results move a ranking, and at the end of each season (a week
  or a month) the top ranks earn tiered rewards, listed in the table below. The Economy
  Balance Analyst adjusts the tier thresholds.
- **PvP region:** while a player is offline, their character appears as an encounter in
  the PvP region for others to fight. The owner earns rewards based on how many
  challengers their character beats and loses nothing when it is beaten. A player who
  beats another player's encounter earns XP.

| Arena rank | Season reward tier |
|---|---|
| 1–10 | Best |
| 11–49 | Amazing |
| 50–99 | Incredible |
| 100–199 | Great |
| 200–499 | Good |
| 500–1000 | OK |

### Boss types

| Type | How it works | Players | Build model |
|---|---|---|---|
| Solo boss | A named, fixed-power enemy that sits outside the feed's power band and can be re-encountered. Damage it takes persists between attempts, so a player can wear it down over several tries. It slowly regains HP over time, so many weak attempts can't chip it down. Defeating it unlocks its loot and marks a milestone. The item that opens the next region drops only if the player beats the boss while their max HP is above 50%, so camping at the floor can't carry anyone to the next region. | 1 | Async. The server stores each player's remaining boss HP. |
| World boss | A rare encounter that only some players come across in the feed. Finding one drops the player into a live fight with others who found it, split into shards so each fight stays a manageable size. Every participant's loadout fights together in one shared simulation, with damage split among the players currently fighting; players watch, with no input during the fight. | Players who find it, grouped into shards | Live. The server runs one shared simulation per shard and streams it. |
| Raid boss | The final boss, found only in the last region. It runs as the same kind of live, shared simulation, open over a longer window so more players can join. | Everyone who joins during the window | Live. The server runs one shared simulation and streams it. |

**Payouts (raid and world bosses):** a player must deal a minimum amount of damage to
qualify for any reward. Above that, damage tiers unlock better prizes, and a group-size
bonus applies up to a cap, so tag-alongs can't inflate payouts. Because platform sign-in
doesn't stop alt accounts, only accounts above a minimum level count toward the group
bonus, and only one account per device earns boss rewards. Fights already in progress
when the boss dies still count toward tiers; players who tap in after the kill see the
card as defeated. A player who drops to 0 HP takes a normal defeat (an armed Revive
brings them back once), and the rest keep fighting. Rewards are never lost to a late
death: a player who dealt enough damage to qualify for a tier receives that tier's
reward when the boss is slain, even if they died first. Players still alive when the
boss dies earn a survival bonus on top.

**Max HP in boss and PvP fights:** losing a world or raid boss fight costs max HP like
any other fight. PvP arena and PvP region fights never cost max HP.

## AI Architecture

Development agents build Doom Scroller; none of them runs inside the shipped game. The
roster below is the starting team, and Miles adds or removes agents as development needs
change. Each agent owns one part of what the player experiences, and its work ships as
C# code or as encounter, enemy and loot data.

| Agent | Role, in one sentence | What the player notices |
|---|---|---|
| Producer | Breaks larger requests into small tasks for the other agents, keeps the memory files current, and flags where the build no longer matches the design. | Features behave as designed. Example: it is the check that would have caught the encounter message board being built but never connected. |
| Gameplay Engineer | Writes and fixes the Unity C# game systems behind the screens: combat, dungeons, loot, crafting, and the Village's shops. | Boss fights, quests, daily missions, and seeded combat where an armed Revive can save a losing fight. |
| UI Engineer | Builds and wires up the interface: new screens, panels, buttons and feed features, using the shared style components. | New things to tap: the swipe-back history, the daily missions panel, the Tavern message board. |
| UI Stylist | Restyles every screen to match the theme in the style guide, which Miles iterates on. | Cards, menus and combat screens that look like one consistent game, and a new look everywhere when the theme changes. |
| Content Designer | Authors new encounter cards, enemies, loot tables, item and ability pools, and Village keeper dialogue as Unity data files, following the narrative brief. | A feed that keeps surprising: more card types, more enemies, more weapon abilities worth building around. |
| Combat Balance Analyst | Runs the combat simulator thousands of times across loadouts and encounters and recommends stat changes. | The win% badge can be trusted, the cards get harder at a steady pace, and a loss stings without stalling progress. |
| Economy Balance Analyst | Owns the economy table: drop rates, prices, crafting costs, gear power curves and region-by-region resource efficiency, and recommends changes. | Upgrades that feel earned, shops worth visiting, and the newest region always being the best place to farm. |
| QA Tester | Writes automated tests against each feature's acceptance criteria and reports what fails. | Fewer broken runs: dungeons lock correctly, rewards always land, HP regenerates as promised. |
| Release Engineer | Writes build scripts, in-app purchase code and store listings; Miles handles store accounts, certificates and dashboards by hand. | The game installs from the App Store and Google Play, and convenience purchases work. |
| Backend Engineer | Builds and runs the server: player profiles and saves, fight resolution, sign-in, purchase receipt validation, chat storage and the live boss service. | Progress that is never lost or faked, fights that resolve fast, and live boss fights that hold up when many players join. |

### How the agents work together on this game

The Gameplay Engineer and the Combat Balance Analyst share the win% badge. The
prototype's badge comes from a separate difficulty number, not from the combat
simulation, so it can disagree with the fight. Retuning enemies cannot fix that across
every loadout, so the badge itself changes:

- **Source:** a fast headless run of the real simulator, about 50–100 times on random
  seeds, using the player's current max HP and armed Revive. Results are cached and
  recomputed when the loadout, armed Revive, region or max HP (in 5% steps) changes.
- **Seed:** the real fight's seed is drawn when the player taps in and saved before the
  fight starts, so the badge stays an honest probability and force-quitting cannot
  reroll a fight.
- **Display:** labeled tiers instead of a number: Likely (70% or better), Risky
  (40–69%) and Long shot (below 40%). Tiers absorb the simulator's sampling noise, so
  the same matchup never shows two different badges. All cards use one fixed seed set so
  comparisons between cards stay consistent. The Combat Balance Analyst can adjust the
  tier boundaries. Close-call% is dropped because damage never carries over between
  fights.
- **Accuracy:** the Combat Balance Analyst checks badge tiers against high-volume
  simulator runs and, after soft launch, against real win rates on the cards players
  actually choose, starting with debuffed players.

The Gameplay Engineer builds it; the Combat Balance Analyst validates it. The player
sees a badge that tells the truth.

Miles directs the team two ways: he can give a task straight to any agent, or hand a
bigger request (or an updated design) to the Producer to split up. The GDD is a living
document Miles edits as the design changes, not a fixed script. Whichever route a task
takes, the Producer updates the memory files after it merges, so every agent starts its
next session with the current design.

The two UI agents split function from look. The UI Engineer builds a screen so it works,
using shared style components without choosing colors or fonts. The UI Stylist changes
only appearance, never behavior, so restyling cannot break a button. The theme lives in
one style guide file that Miles edits; the Stylist then applies the change across every
screen.

### Why no runtime AI

A feed of short sessions would multiply AI calls by every card every player scrolls. The
multiplayer server will relay player chat and shared boss state, but no AI runs on it or
on the device. Keeping all AI work at development time means zero per-player AI cost and
identical results on every device. Tavern chat is written only by players, never
generated.

## Technical Strategy

The agents run as Claude Code subagents inside the Unity project, one feature at a time,
with Miles approving every merge. Estimated cost: about 10.5M tokens for the semester,
with each feature sized small, medium or large.

**Stack:** Unity 6, C#, one Screen Space - Overlay canvas, targeting iOS and Android. The
game is online-only. The server resolves every fight, and the device shows it (see
[Where combat runs](#where-combat-runs)). A backend server (chosen by week 2) owns the
player profile: the save, gold and gear, max HP and its regen clock, dungeon run state,
and the daily reset. It draws each fight's seed, runs the fight simulation and records
the result before the replay starts, and it also stores Tavern chat, Boss HP pools,
participant lists and payouts. Players sign in with Game Center (iOS) or Google Play
Games (Android), which supports chat blocking. Sign-in alone doesn't stop alt accounts,
so boss rewards add a level gate and a per-device limit. The prototype has no network
code yet.

### Where combat runs

The server runs the real fight simulation, and the device runs the win% badge and the
fight replay. This keeps results cheat-proof without putting the expensive work on the
server.

| Work | Runs on | Why |
|---|---|---|
| Fight simulation (the real result) | Server | A modified app can't report wins it never earned, so loot, PvP rankings and boss damage stay trustworthy. One fight is a few milliseconds of headless math. |
| Win% badge (50–100 practice simulations per card) | Device | It's the most CPU-heavy work, and it only informs the player, so tampering with it gains nothing. Keeping it on the phone keeps it off the server bill. |
| Fight replay (animation) | Device | The server sends back the fight's event log (each attack, hit, miss and damage number); the device plays that log, so the replay always matches the real result. |
| Live world and raid bosses | Server | One shared simulation per shard is run and streamed to every participant. |

- **One shared combat library.** The simulator is a plain C# library with no Unity
  dependencies. The server uses it to resolve fights, and the app uses the same code for
  the badge, so combat is written once. The week-2 backend must run C#, such as Unity
  Cloud Code or a self-hosted .NET service.
- **Latency:** each fight costs one round trip to the server, which the hold-to-commit
  press mostly hides.

### Scaling and cost control

The server is built so most of it grows by adding copies, and costs rise only with
actual players.

- **Stateless fight servers.** Each fight request is self-contained: get a seed,
  simulate, save, respond. Any server copy can handle any request, so a load balancer
  adds copies automatically as traffic grows and removes them when it drops.
- **Managed database first.** The database is usually the first bottleneck, since every
  fight writes to a player profile. Use a managed database that scales without upkeep,
  save one record per fight rather than many small updates, and cache often-read data
  (the economy table, region and enemy data) in memory.
- **Chat by channel.** Each village Tavern is its own chat channel, and messages load in
  pages, never as a full history.
- **PvP as ordinary fights.** Arena and PvP region fights run against saved loadouts like
  any other fight. Rankings use a ready-made sorted leaderboard, such as the backend's
  leaderboard service or Redis.
- **Live bosses are the only stateful part.** Each world boss shard caps its player
  count, and a new shard opens on whichever server has room when one fills. Raid boss
  traffic comes in bursts, so each raid uses a join window, a per-instance player cap
  and extra capacity added just for the event.
- **Pay-as-you-go hosting.** Usage-based pricing keeps the bill small while the player
  count is small. A budget alert with the host catches spikes or runaway bugs early.
- **Load-test before soft launch.** Simulated players measure real fights per second and
  database load, so capacity is sized from data rather than guesses.

### Soft launch plan

The soft launch tests whether players come back and whether sessions land in the target
range.

- **Success measures:** D1 and D7 retention (share of players returning the next day and
  a week later) and average active session length against the 20–30 minute energy
  target. Both are logged from day one.
- **Hosting budget:** under $50 per month on pay-as-you-go hosting, with a budget alert.
  The pre-launch load test confirms the soft launch fits inside it.
- **Store compliance:** in-app account deletion, a privacy policy and data disclosures,
  an age rating, and timely action on chat reports. The Release Engineer prepares these;
  Miles submits them.
- **After the semester:** hosting continues while purchases cover the server bill. If
  they don't, the game gets a shutdown date announced 30 days ahead and is delisted,
  since an online-only game stops working when its server does.

### Agent roles and setup

| Agent | Model tier | Reads | Writes |
|---|---|---|---|
| Producer | Largest (planning) | GDD, task board, test reports | Task specs, GDD and memory updates |
| Gameplay Engineer | Mid | Task spec, the scripts it names | Game-system C# scripts |
| UI Engineer | Mid | Task spec, the UI scripts and prefabs it names | UI scripts, screens and prefabs |
| UI Stylist | Mid | Style guide, the screens to restyle | Colors, fonts, sprites, layout values |
| Content Designer | Mid | Task spec, existing data assets | Encounter, enemy and loot data |
| Combat Balance Analyst | Mid | Simulator output summaries | Tuning recommendations |
| Economy Balance Analyst | Mid | Economy table, loot and shop data, simulated economy summaries | Economy table updates, tuning recommendations |
| QA Tester | Small | Acceptance criteria, changed files | Unity tests, failure reports |
| Release Engineer | Mid | Task spec, build scripts, store requirements | Build scripts, purchase code, store listings |
| Backend Engineer | Mid | Task spec, server code, data model, shared combat library | Server code, data model and migrations, sign-in, receipt validation, hosting config |

A larger feature moves through the team in order: Miles or the Producer writes the spec
→ Gameplay Engineer, UI Engineer and Content Designer build → UI Stylist applies the
theme → Combat and Economy Balance Analysts check the numbers → QA tests → Miles reviews
and merges.

### Token budget (one medium feature)

| Agent | Input tokens | Output tokens |
|---|---|---|
| Producer | 20k | 4k |
| Gameplay Engineer | 100k | 16k |
| UI Engineer | 60k | 12k |
| UI Stylist | 40k | 8k |
| Content Designer | 40k | 12k |
| Combat Balance Analyst | 30k | 5k |
| Economy Balance Analyst | 30k | 5k |
| QA Tester | 60k | 10k |
| Release Engineer (release features only) | 50k | 8k |
| Backend Engineer (server features only) | 80k | 14k |
| **Total** | **380k** | **72k** |

Features are sized small, medium or large. A medium feature costs about 452k tokens (the
table above), a small one about half (≈226k) and a large one about double (≈905k). The
Release Engineer's tokens count only on release features, and the Backend Engineer's
only on server features.

| Size | Features |
|---|---|
| Large (≈905k) | Backend and persistence · feed generator with regions and solo bosses · live boss system · soft launch and in-app purchases |
| Medium (≈452k) | Build and test pipeline · simulator-based badge · Tavern chat and moderation · additional regions · enemy abilities · new encounter cards and loot |
| Small (≈226k) | Economy table · Quest Board daily missions · Events card type |

Total: 4 large (≈3.6M) + 6 medium (≈2.7M) + 3 small (≈0.7M) ≈ 7.0M tokens, or about
10.5M with a 1.5× rework allowance. This is an upper estimate: features with no UI work
skip both UI agents, and prompt caching lowers the cost of files the agents re-read
every task, such as the GDD and CLAUDE.md. Adding or removing an agent adds or removes
its row.

### Constraints

- **Context window.** The whole codebase and GDD will not fit in one agent's context,
  and an overfull context produces confident wrong edits. Each task spec names the files
  an agent may read, and CLAUDE.md stays short.
- **Usage limits.** Running several agents at once drains the plan's usage limit
  quickly. At most two agents run in parallel, and neither Balance Analyst spends tokens
  on simulation: the thousands of fights run as local C# in Unity batch mode, and the
  agent reads only the summary table.
- **No hands in the Unity Editor.** Agents can edit files but cannot click through the
  Editor, and the build pipeline is not configured yet. Setting up command-line builds
  and tests is therefore the first task, because QA and balancing both depend on it.
  Store accounts, signing certificates and store dashboards are manual work for Miles;
  the Release Engineer prepares everything around them.
- **No runtime API.** Shipping without AI calls removes rate limits, latency and
  per-player cost from the live game entirely.
- **Multiplayer backend.** A server is the largest new piece of work this semester;
  Miles will choose the backend service by week 2, and it must support live streamed
  boss fights because the live boss system is protected scope. The Backend Engineer owns
  all server work. The server's main job is the player profile, so it stays essential
  even if multiplayer features slip. Solo bosses stay asynchronous: the server only
  stores each player's remaining boss HP. World and raid bosses are the live systems:
  the server runs one shared simulation per fight (per shard for world bosses) and
  streams it to every participant, so it is built after the core feed and solo bosses.

### Persistence and save rules

- **Fights:** the server draws the seed and saves the result the moment the player taps
  in, before the replay starts, so deleting or reinstalling the app can't reroll a
  fight. Closing or force-quitting the app changes nothing; the saved result still
  applies. There is no retreat.
- **Dungeons:** run state saves at every room change. Leaving means only tapping Leave or
  swiping down. Closing the app, or leaving it in the background for more than 3
  minutes, ends the run, the same as leaving. The 3-minute grace period covers calls and
  notifications. A lost connection counts the same as closing the app, so a dropped
  signal for longer than 3 minutes also ends the run. Equipment can't change during a
  run. A failed run keeps loot from cleared rooms and forfeits the final reward.
- **Regeneration:** measured against server time, so changing the device clock does
  nothing. The daily mission reset uses the same server clock.
- **Owner:** the Backend Engineer, with the Gameplay Engineer handling the app side. The
  rules live in `memory/systems.md`.
- **Dropped connections:** once a fight is committed, the server keeps its seed and
  result. If the connection drops before the result reaches the phone, the replay plays
  the same fight when the player reconnects; it is never re-rolled or lost.
- **Live bosses:** a player who disconnects keeps fighting in the server's simulation
  and can rejoin the stream. Their damage and reward tier count normally.
- **Revive potions:** an armed Revive is consumed only if it actually fires. If the
  player wins without needing it, they keep it.

### Token efficiency

Every agent finishes a task and reports in five lines or fewer:

1. What changed
2. Files touched
3. Test result
4. Anything left undone
5. Questions for Miles, if any

Agents do not narrate their steps, restate the task, or paste code that is already in
the files. The output column of the token budget assumes this rule; a verbose agent
could double it.

### Memory and context

Every session starts with a cleared context. Instead of carrying old conversations,
agents load a small set of memory files that describe the game as it is now:

| File | What it holds |
|---|---|
| `CLAUDE.md` | What Doom Scroller is, the stack, build and test commands, and the reporting rule above |
| `memory/systems.md` | One short entry per system (combat, feed, dungeons, loot, crafting, Village): what it does, how it works, its key files |
| `memory/content.md` | How encounter, enemy and loot data are structured and where they live |
| `memory/ui-style.md` | The current theme: palette, fonts, spacing, card and button styles. Miles edits this to iterate on the look |
| One definition file per agent | That agent's role and which memory files it loads |

A `memory/narrative.md` file, owned by Miles like the style guide and written in week 1,
before any content work, holds the tone, premise, naming rules and sample card text; the
Content Designer loads it before writing anything. These files describe the current
game, not a history: they never hold a log of what each agent did. When a feature
merges, the Producer rewrites the affected entry in place, so the files stay short. Each
agent loads only the files its task spec names, which keeps every session's starting
context small and cached.

## Semester scope

**Must, in build order:**

1. Command-line build and test pipeline
2. Economy table: prices, drop rates, potion values and pacing targets, set before the
   server data model is designed
3. Backend and persistence, built together as one workstream (service chosen by week 2;
   it must support live streamed boss fights). It may start before item 2 by storing the
   current save as one document; the proper data model still waits for item 2
4. Simulator-based win% badge
5. Feed generator with two regions, swipe pressure, first-session rules and solo bosses
6. Live boss system: sharded world bosses and the final raid boss
7. Soft launch on the App Store and Google Play in a few countries, with in-app
   purchases for convenience items and store compliance (account deletion, privacy
   disclosures, age rating)
8. Tavern player chat with filter, reporting, blocking and a moderation queue
9. Quest Board daily missions

**Cut line:** Items 1–7 are the core game and stay. 8 and 9 can be reworked if out of
scope. If store review, signing or purchase testing delays item 7, the semester ships
TestFlight and Google Play internal-test builds instead, and the store soft launch
follows right after the semester.

**Should:** additional regions · enemy abilities · new encounter cards and loot · Events
card type

**Stretch:** PvP arena and PvP region · clans and trading
