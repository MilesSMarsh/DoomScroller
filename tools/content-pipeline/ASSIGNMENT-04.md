# Assignment 04: Dynamic Content Pipeline (Doom Scroller)

**Game:** Doom Scroller, a mobile RPG that plays like a social feed. Every card is an
encounter, and fights are simulated from the player's gear.
**Pipeline:** `tools/content-pipeline/` (Python, standard library only, calling Claude
through Claude Code's headless mode). See [README.md](README.md) to run it.
**Run used for submission:** `runs/20261110-021519` (report:
[runs/20261110-021519/report.md](runs/20261110-021519/report.md)).

## How it works

1. **Reads the game first.** Each call loads the narrative brief
   (`memory/narrative.md`: tone, world, naming rules, voices, sample text), six sections
   of the GDD, the content data guide, and a catalog parsed straight from the Unity
   assets: every encounter, weapon, ability, material and recipe, with their real stats.
   It refuses to run while the brief is still an empty template.
2. **Generator agent** (Sonnet) drafts content as JSON, using the same fields as the
   game's data types.
3. **Code checks** (`checks.py`) verify hard rules: naming conventions, no name clashes,
   stats inside the game's real power bands, every referenced ability or material
   exists, recipe trees have no cycles, and the brief's limits (titles ≤35 characters, no
   hyphens, banned feed words).
4. **Critic agent** (Opus, a different model from the generator) reviews against the
   brief and the GDD and grades each issue blocker, major or minor.
5. **Loop:** any check error or blocker/major issue goes back to the generator with the
   critique. The loop repeats until approved or until it runs out of rounds, and then
   the stage is flagged for me.
6. **Install** (`install.py`) writes approved content as real Unity `.asset` files. It
   re-checks everything first and refuses to leave a reference to a deleted item.

## What it generated (three outputs the game needed)

| Output | What | Why the game needed it |
|---|---|---|
| **1. Gear list** | 24 weapons (8 Melee, 8 Ranged, 8 Magic), including a new starter weapon, plus 12 armor, shield and talisman pieces and 35 Blacksmith recipes, themed to Green Valley and Forest | It replaced placeholder prototype gear that had no world behind it. Every piece except the starter now has a recipe. |
| **2. Region 1 + solo boss** | Green Valley: difficulty range, which existing cards belong there, and the boss *Ulmar the Storm Roc*, with stats, weaknesses, intro/victory/defeat lines and the *Roc Pinion* key item | Regions with solo bosses are the next Must-scope feature (GDD build order #5) and had no data |
| **3. Encounter cards** | 5 new Green Valley cards (Grisk Goblin, Burrow Badger, Dewdrop Slime, Goblin Raiding Party, Wild Herb Patch), plus new loot for the 6 existing cards that used to drop the deleted gear | The starting region needed goblins and farmland creatures from the brief, and the old loot links had to keep working |

All of it is installed and the game's 215 EditMode tests pass.

## Does it sound like the game?

Mostly yes. The brief asks for plain, sincere, classic fantasy, and the critic named
lines like these as the closest match to the game's voice:

- *"A long harvest scythe that cuts as cleanly through goblins as through wheat."* (Wheatcutter Scythe)
- *"A roc so vast its wings shade whole farms, with one great pinion longer than a barn beam."* (the boss card, which hints at its key item as the brief asks)
- *"A tough jerkin of boar hide, stitched to turn aside tusks and quills."* (it ties the gear to the Quillback Boar already in the game)
- *"A lantern whose pale flame drives back the skeletons of the old woods."*

The gear reads as coming from *this* world: farm tools turned weapons in Green Valley
(Threshing Flail, Gatepost Buckler, Furrow Boots), and root, moss and golem-stone in the
Forest. Weaknesses I found reviewing it:

- Two cards (Grisk Goblin, Goblin Raiding Party) reuse the brief's own sample lines word
  for word instead of writing new ones.
- A few minor issues were left in. For example, the critic noted that a badger drops
  Wolf Pelt and a goblin "spear" party drops a flail. These are listed in the report
  as minors I can fix by hand.

## What the critic caught

**Run 1 failed, and that was useful** (`runs/20261110-020435`). The weapons stage was
not approved after 3 rounds (must-fix issues per round: 9, then 3, then 6). Its catches were real:

- Seven Magic weapons collapsed into "three reskinned pairs or trios" with identical
  ability pools.
- The strongest ability in the game (*Oblivion*) was on a humble starter-region wand.
- Animals dropped crafted bows: "Animals don't carry bows."
- Frost weapons appeared in the Forest, using a material that only comes from
  mountain content.
- A fire ability sat on a living-wood staff carried by tree people.
- Every pool had exactly 4 entries, so every top-rarity weapon rolled the same kit. That
  breaks the GDD's rule that ability lists are pools, not fixed kits.

The failure also exposed two flaws in the loop itself, which I fixed:

- The critic demanded unique ability pools the catalog can't support (only 6 Ranged
  abilities exist for 8 bows).
- It re-reviewed from scratch every round, so each revision raised new complaints.

The fix gives the critic its previous review, so it confirms what was fixed and doesn't
move the goalposts. It also tells both agents the catalog's limits and explains that
`attackSpeed` is attacks per second. The critic had first misread it, which skewed its
damage-per-second maths.

**Run 2 approved every stage in 1–2 rounds.** Catches that sent drafts back:

| Stage | Caught by | Issue |
|---|---|---|
| Weapons | code checks | 6 damage values above the game's band (Golembreaker Maul, Ironbolt Arbalest, Elderbough Longbow) |
| Weapons | critic (major) | A fire bow dropped by tree people; a Green Valley staff crafted from a mountain-only material |
| Recipes | critic (major) | A Green Valley staff that needed Frost Shards; a starter-region blowgun that needed Venom Sac, which only the Forest spider drops |
| Region | critic (major, GDD rule) | The boss's victory line said the key item drops on every win, but the GDD drops it only if the player wins above 50% max HP |

Minor catches it reported without blocking:

- "Gatepost" hides the banned word "post", which the code check missed because it
  matches whole words only.
- A roc dropping iron ingots and rope.
- The key item described as two different lengths.
- "Marsh Leech" placed in farmland.
- Existing data bugs outside the pipeline's scope: the Grey Wolf card always drops
  Exalted gear, and the Bonewrought Depths dungeon pays 10,000 gold.

In earlier test runs made before the brief was written, the critic refused to approve
anything and cited the empty brief as a blocker. That's the behavior the assignment
asks for: no placeholder lore gets through.

## Cost

About $6 of model usage for run 2 and $2.40 for run 1 (as reported by Claude Code;
paid through a subscription, so no API key is needed).
