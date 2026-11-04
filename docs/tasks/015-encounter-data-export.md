# 015 — Encounter combat data the server can read

**Owner:** gameplay-engineer
**Size:** S
**Status:** todo (blocked on 008 and 006)
**GDD:** docs/gdd/GDD.md#scaling-and-cost-control (cache static data)
**Depends on:** 006, 008

## Goal
The server knows each encounter's enemy stats from the same authored assets the game
uses, so a fight request only has to name the encounter.

## Acceptance criteria
- [ ] Every `EncounterData` has a stable id: its asset name under
      `Resources/Encounters` (stable after 006's naming clean-up), exposed as
      `EncounterData.Id`; no new serialized field.
- [ ] An Editor menu item and a batch-mode method write
      `Server/data/encounters.json`: id, type, enemy HP, attack damage, attack speed,
      damage-type multipliers, for every fight-type encounter.
- [ ] EditMode test: the export of the current assets has one entry per fight-type
      encounter, unique ids, and values equal to the assets; a second test fails if the
      committed `encounters.json` differs from a fresh export (stale-data guard).

## Context
- Memory: memory/content.md (encounters)
- May read: `Feed/EncounterData.cs`, `Feed/EncounterType.cs`,
  `Assets/Scripts/CombatCore/` (input types), `Assets/Resources/Encounters/` (listing)
- May write: `EncounterData.cs` (Id property only), new
  `Assets/Scripts/Editor/EncounterDataExporter.cs` (Editor-only assembly if one is
  needed), `Server/data/encounters.json`, new test file

## Out of scope
- Dungeon rooms and resource nodes (stay device-resolved in this slice), loot tables.
