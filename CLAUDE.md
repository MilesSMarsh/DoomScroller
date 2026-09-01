# CLAUDE.md

This file gives Claude Code persistent context for this project. It's read
automatically at the start of every session — keep it short enough to skim in
under a minute. Move anything longer or topic-specific into `.claude/rules/`
instead of growing this file indefinitely.

## Project

Doom Scroller — a mobile MMORPG (iOS + Android) built in Unity that replicates
scrolling a social feed, where each "post" is a combat encounter.

Full design vision, current scope, and phase-by-phase acceptance criteria live in
[`GAME_DESIGN.md`](./GAME_DESIGN.md). Always check which phase is current there
before starting new work — implement phases in order, and don't build
deprioritized/future-scope features early.

## Tech Stack

- Engine: Unity (target Unity 6.x LTS)
- Language: C#
- Target platforms: iOS, Android
- Version control: Git + Git LFS (binary assets — textures, models, audio — are
  tracked via LFS; confirm `.gitattributes` before adding new binary asset types)

## Build & Test Commands

<!-- Fill these in with your actual commands once your build pipeline exists -->
- Build (Android): `<fill in>`
- Build (iOS): `<fill in>`
- Run Play Mode tests: `<fill in>`
- Run Edit Mode tests: `<fill in>`

## Conventions

<!-- Add project-specific conventions as they solidify. Starting points: -->
- Namespace scripts under `DoomScroller.<System>` (e.g. `DoomScroller.Combat`,
  `DoomScroller.Inventory`)
- Keep MonoBehaviours thin — push logic into plain C# classes/services where
  reasonable so it's testable without a running scene
- All new visuals start as greybox/placeholder primitives, matching the process
  used on the Bevy project — don't spend time on final art before a mechanic is
  proven

## Working Practices

- Before implementing a new feature, check `GAME_DESIGN.md` for the current
  phase and its acceptance criteria — don't jump ahead to later phases
- Ask before large refactors or restructuring across many files — propose the
  plan first
- When a Unity API call fails or behaves unexpectedly, check the Unity console
  via the MCP connection rather than guessing at the cause

## MCP Tools Available

This project uses **MCP for Unity** (CoplayDev) for live Editor access —
scene hierarchy, GameObjects, console output, script editing, and more.
Use these tools directly instead of asking me to copy-paste console errors or
Inspector values.

## Notes

- This is a solo/personal project — no team conventions to preserve beyond
  what's written here
