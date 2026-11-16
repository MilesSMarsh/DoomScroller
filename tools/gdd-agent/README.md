# Assignment 05: Goal-Oriented Coding Agent (Doom Scroller)

**Game:** Doom Scroller, a mobile RPG in Unity that plays like a social feed. Every card
is an encounter, and fights are simulated from the player's gear.
**Agent:** `tools/gdd-agent/agent.py` (Python, standard library only) plus its
requirements file `features.json`. Code is written by Claude through Claude Code's
headless mode (`claude -p`), using the same wrapper as assignment 04
(`tools/content-pipeline/llm.py`).
**Run used for submission:** `runs/20261115-184647/` holds the exact prompt and reply
for each file the agent wrote.

```
python3 tools/gdd-agent/agent.py --plan     # scan, gaps and priorities only; writes nothing
python3 tools/gdd-agent/agent.py            # run the loop and write code
```

## How it works

1. **Reads the GDD.** It splits `docs/gdd/GDD.md` into sections by heading and pulls the
   numbered "Must, in build order" list from the Semester scope. Each requirement in
   `features.json` names the GDD section it comes from and keywords from it. The agent
   refuses to run if a cited section or keyword is missing from the GDD, and passes that
   section's text to the code writer as the spec.
2. **Scans the codebase.** It reads every `.cs` file under `Assets/Scripts`,
   `Assets/Tests` and `Server/src`, and records which classes, structs, enums and
   records exist.
3. **Gap detection.** Each feature is broken into steps of one file each. A step is done
   when its checks pass against the scan: a type exists, a stub marker is gone (for
   example "Quests are coming soon"), or a reference is present. A feature's open gap is
   its first unfinished step. The agent also checks each feature's prerequisites, such
   as a server chat service for Tavern chat, using the same scan.
4. **Priority scoring** (`priority_score()`):
   - Tier: Must +100, Should +40, Stretch +10.
   - Build order: +3 for each place earlier in the GDD's Must list.
   - Blocked: −200 if a prerequisite doesn't exist yet.
   - Size: S +15, M +5, L −15, since each step writes one file.
   - Already started: +25, so the agent finishes a feature before starting another.
   - Player-facing: +5.

   Should and Stretch features are deferred unless `--include-should` is passed, because
   the project's rules say not to build them early.
5. **Code-writing step.** The top buildable gap goes to Claude Sonnet. The prompt holds
   the GDD section, the project's Unity conventions, the step's brief and acceptance
   checks, the files written earlier for this feature, and the existing code it has to
   compile against. The reply is checked against a JSON schema (path, content, summary)
   and written to the planned path.
6. **Iteration loop.** After every write the agent scans again, confirms the step's
   checks now pass (or retries, up to 2 tries), and runs the headless EditMode tests
   when Unity isn't open. It stops when no buildable Must requirement is left, and lists
   what is still blocked and what was deferred.

## What the agent built: Quest Board daily missions

Across 3 iterations it wrote 3 files:

| File | What it is |
|---|---|
| `Assets/Scripts/Progression/DailyMissions.cs` | `DailyMissionBoard`: 3 missions a day ("Defeat N enemies", "Clear a dungeon"), rolled from a seed based on the UTC date so every player gets the same missions. It tracks progress, pays the gold reward once, resets on a new day, and can save and load its state. |
| `Assets/Tests/EditMode/DailyMissionBoardTests.cs` | 14 EditMode tests: same date gives the same missions, progress stops at the target, a reward is claimed only once, a new day resets, and state saves and loads correctly. |
| `Assets/Scripts/Encounters/VillageQuestBoardView.cs` | Replaces the Quest Board's "Quests are coming soon" stub with a Daily Missions list: each mission shows its progress and a Claim button that pays out gold. |

On the next scan every step passed, so the agent stopped:

> DONE: every buildable Must requirement in the GDD is met. Blocked until their
> prerequisites exist: regions_swipe_pressure, live_bosses, tavern_chat. Deferred
> (Should/Stretch): pvp_arena, events_card.

**To see it in the game:** press Play, then open the Village and swipe to the Quest
Board. Win a fight against an enemy card or clear a dungeon, return to the Quest Board,
and claim the reward. Your gold goes up.

## Why the agent picked it

These are the agent's scores from its first scan:

```
   123  daily_missions          Must +100; build order #9 +3; size S +15; player-facing +5
     0  pvp_arena               Stretch +10; size L -15; player-facing +5        (deferred)
 x -95  regions_swipe_pressure  Must #5, but blocked: the game has no regions yet
 x -98  live_bosses             Must #6, but blocked: no live boss service on the server
 x-104  tavern_chat             Must #8, but blocked: no chat service on the server
 x-150  events_card             Should, blocked on regions
```

Items 1–4 of the GDD's Must list (build pipeline, economy table, backend, win% badge)
are already built. Daily missions (Must #9) is the only remaining Must feature whose
prerequisites, the save system and the Village screen, already exist. It is also small,
so it fits the agent's one-file-per-step loop. The GDD puts regions, live bosses and
chat earlier in the build order, but each of those needs a system that doesn't exist yet.
The -200 penalty makes the agent work around them instead of writing code that can't
run.

## Manual changes before accepting it

I accepted the three generated files as written; all 14 of their tests passed the first
time. Connecting the feature to the rest of the game took these hand edits:

1. **`Progression/DailyMissionTracker.cs` (new, 30 lines).** Holds the shared board for
   the current UTC day, so the fight screens and the Quest Board use the same one.
2. **`Encounters/CombatResolutionView.cs`.** On a victory it calls
   `RecordEnemyDefeated()`. I added a guard so opening a Treasure card, which counts as an
   instant "victory" in that view, doesn't count as defeating an enemy.
3. **`Encounters/DungeonView.cs`.** Clearing a dungeon's final room calls
   `RecordDungeonCleared()`.
4. **`Encounters/VillageView.cs`.** Keeps a reference to the Quest Board page and calls
   `Bind(DailyMissionTracker.Board, wallet)` each time the Village opens.
5. **Saving progress.** I added a `dailyMissions` field to `SaveData`, and
   `ProfileController` now saves the board's `CaptureState()` with the profile and loads
   it with `LoadState()`. The agent had already written both methods, and a save from an
   earlier UTC day is ignored.

Play-testing also turned up two bugs that weren't in the agent's code but came from the
in-progress server-save work. I fixed both before accepting:
- **Max HP penalty didn't stick.** The server ignored the HP the game sent, but fights
  still run on the device for now. The server now accepts a *lower* current max HP (down
  to the 20% floor), so the game can report a loss but can never heal itself.
- **The save looked reset on every launch.** The profile loads from the server after the
  screens are built, and nothing told the screens to redraw. They now refresh once the
  load finishes.

**Verification:**
- **Unity:** the full EditMode suite passes, 522 of 522, including the agent's 14.
- **Server:** the tests pass, 34 of 34.
- **Play-tested:** mission progress goes up after a win.

**Known limits (left for later tasks):**
- The day comes from the device clock. The GDD wants the server clock, which already
  exists for HP regeneration.
