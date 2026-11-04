# CLAUDE.md

Doom Scroller — a mobile RPG (iOS + Android) in Unity that plays like a social feed:
every post is an encounter; fights are simulated from the player's loadout and replayed.
Online-only, async multiplayer, no AI calls at runtime. Miles is creative director.

## Where things are

- `docs/gdd/GDD.md` — the design (what to build). Follow its Semester scope build order.
- `memory/systems.md` — each system as it is now, key files, and gaps vs. the GDD.
- `memory/content.md` — how encounter/enemy/loot data is structured and where it lives.
- `memory/ui-style.md`, `memory/narrative.md` — theme and narrative brief (Miles owns).
- `docs/tasks/` — task board (one spec per unit of work; `done/` for merged).
- `.claude/agents/` — the agent team; `.claude/rules/` — conventions and workflow.
- Load only the files your task spec names.

## Stack

Unity 6000.5.10f1, C#, UGUI single Screen Space - Overlay canvas; iOS + Android.
Git + Git LFS for binary assets (check `.gitattributes` before adding new types).

## Build & Test Commands

- Edit Mode tests (headless): `tools/run-editmode-tests.sh [testFilter]`
  — exits 2 if the Editor has the project open; use Unity MCP then
- Server tests (headless): `tools/run-server-tests.sh`
- Server local dev (Docker Compose + Postgres): `tools/run-server-dev.sh` → http://localhost:8088/health
- Play Mode tests: none yet
- Build (Android / iOS): not configured yet

## Reporting rule

Every agent finishes a task and reports in five lines or fewer:
1. What changed  2. Files touched  3. Test result  4. Anything left undone
5. Questions for Miles, if any.
No narrating steps, restating the task, or pasting code that's already in the files.

## Working practices

- Ask before large refactors across many files — propose the plan first.
- Don't build Should/Stretch scope early.
- When a Unity API call misbehaves, check the console via Unity MCP (CoplayDev MCP for
  Unity; needs the Editor open) instead of guessing.
