# Content pipeline

Generates Doom Scroller content from the game's own docs, checks it, has a critic agent
review it, revises until it passes, and installs the result as real Unity assets.
Authoring-time only: the shipped game makes no AI calls.

## What it generates

| Stage | Output | Lands in the game as |
|---|---|---|
| `weapons` | `WeaponDefinition`s per category (Melee/Ranged/Magic), themed to Green Valley or Forest, with ability/passive *pools* from the existing catalog; with `--replace`, one is the new-game starter | `Assets/Resources/WeaponDefinitions/*.asset` |
| `equipment` | `EquipmentDefinition`s per slot (armor, shields, talismans) with passive pools | `Assets/Resources/EquipmentDefinitions/*.asset` |
| `recipes` | One Blacksmith recipe per new item (not the starter): rarity by region, cost and ingredients in the existing ranges, a prerequisite tree | `Assets/Resources/RecipeDefinitions/*.asset` |
| `loot` | With `--replace`: new drops for every existing card that dropped a deleted item | edits those cards' loot tables |
| `region` | Region 1 from the brief's World table (difficulty range, which existing cards belong) and its solo boss (stats, weaknesses, loot, intro/victory/defeat lines, the key item) | `docs/content/regions/<id>.json`, the source data for the feed generator (GDD Must #5) |
| `encounters` | New feed cards (Enemy / Ambush / Resource / Treasure) | `Assets/Resources/Encounters/*.asset` (the feed picks them up automatically) |

Stages run in that order, and each one reads the stages approved before it, so recipes
craft the new gear, cards drop it, and new cards sit inside the region's range.

**Replace mode** (`--replace weapons,equipment`): the old items and the recipes that
craft them are hidden from the agents and deleted on install; the new list is the
game's whole list. Stat ranges the old items defined are kept in `bands.json` (owned by
the Economy Balance Analyst) so the checks still have a band after the deletion.

## How it works

```
memory/narrative.md ─┐
docs/gdd/GDD.md (6 sections) ─┤
memory/content.md ─┤──► generator agent ──► JSON draft
Assets/Resources/*.asset ─┘   (sonnet)          │
     (parsed into a catalog)                    ▼
                                    code checks (checks.py)
                                    critic agent (opus, prompts/critic.md)
                                                │
                     errors or blocker/major ◄──┤──► approved ──► final.json
                     → revise (max --rounds)                       │
                                                         install.py ──► Unity assets
```

- **Source material** (`pipeline.py`): the narrative brief, the GDD sections on the
  summary/tone, feed generation, encounter types, combat, progression and bosses, the
  content data guide, and a catalog of every encounter, weapon, ability, passive and
  material, parsed straight from the `.asset` files (`catalog.py`). The generator works
  from what's really in the game, not from a description of it.
- **Code checks** (`checks.py`): asset ids and naming conventions; no name collisions with
  existing content; numbers inside the game's real power band (region range inside the
  current game's, enemy cards inside the region's, boss above it as the GDD requires,
  weapon damage inside the existing rarity curve); every referenced ability, passive,
  material and weapon exists and is allowed on that weapon type; ability pools of at
  least 4 (an Exalted roll draws 4); every new weapon drops from somewhere; one-sentence
  card text; titles ≤ 35 characters; no hyphens in names; no rarity word in a
  weapon's stored name; enemy names ≤ 3 words; none of the brief's banned words in
  player-facing text (info cards exempt); no placeholder text.
- **Critic agent** (`prompts/critic.md`, a separate model from the generator): checks
  GDD rules, tone and lore against the brief, consistency with existing content, balance
  for the role, and duplication. It rates issues blocker / major / minor and names
  the best line in `soundsLikeTheGame`.
- **Loop:** any check error or blocker/major critique goes back to the generator with
  the draft, the check results and the critique. A stage that's still failing after
  `--rounds` is marked `needs-review` and the run stops (escalate to Miles).
- **Brief gate:** the pipeline refuses to run while `memory/narrative.md` is the empty
  template, because the output would be generic fantasy.

## Running it

Needs Python 3.9+ (stdlib only) and Claude Code (`claude`) installed and signed in; it
calls `claude -p` headless, with no tools and no project context beyond what it passes.

```bash
python3 tools/content-pipeline/pipeline.py                    # all stages, adds to the game
python3 tools/content-pipeline/pipeline.py --replace weapons,equipment --per-type 8   # re-author gear
python3 tools/content-pipeline/pipeline.py --stages weapons,encounters \
        --from-run tools/content-pipeline/runs/<run>          # reuse an approved region
python3 tools/content-pipeline/install.py tools/content-pipeline/runs/<run> --dry-run
python3 tools/content-pipeline/install.py tools/content-pipeline/runs/<run>
python3 -m unittest discover -s tools/content-pipeline/tests  # offline tests, no LLM
```

Options: `--per-type N` (weapons per category) or `--weapons N`, `--equipment-plan
Head:2,Chest:2,…`, `--encounters N`, `--replace`, `--rounds N` (default 3),
`--gen-model`, `--critic-model`, `--keep-going`, `--out DIR`.

Each run writes `runs/<timestamp>/`: every round's `draft`, `checks` and `critique`
JSON per stage, `final.json`, `run.json` (status, rounds, cost) and **`report.md`**, a
readable record of what the checks and critic caught each round, plus the final
content.

`install.py` installs only approved stages, re-validates them against the current assets,
never overwrites, and writes `.asset` + `.meta` files in Unity's own format (new
GUIDs; references to existing assets by their GUIDs). In replace mode it deletes the old
assets, patches the rewired cards, points scene references to the old starter at the new
one, and refuses to write anything if a reference to a deleted asset would survive. New cards have no artwork and fall
back to their `cardColor`. Run the EditMode tests after installing.
