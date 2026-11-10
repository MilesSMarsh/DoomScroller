## Task: Blacksmith recipes for the new gear

Write exactly one `RecipeDefinition` for every new weapon and equipment piece in
"Earlier stages", except the starter weapon (it's given at new game, never crafted).

- `id` is `Recipe_<output id>`; `outputKind` Weapon or Equipment; `output` the item id.
- `outputRarity`: Green Valley gear crafts at Plain or Tempered; Forest gear at
  Tempered, Runed or Relic. Nothing crafts at Exalted.
- `goldCost` and the total ingredient count follow `recipeBand` for that rarity.
- `ingredients`: 1–4 existing materials (catalog ids) that make sense for the item and
  its region (Green Valley: Wood, Fibers, Stone, Iron, Wolf Pelt, Healing Herbs…;
  Forest: Aged Timber, Spider Silk, Bone, Steel, Arcane Gem…). The existing material
  recipes (Rope, Silver Ingot, Steel Ingot) turn raw materials into refined ones.
- `prerequisites`: optional ids of recipes the player must have crafted first, from
  this batch or the existing material recipes, so the tree climbs from Green Valley
  basics to Forest gear. A prerequisite never crafts a higher rarity; no cycles.
