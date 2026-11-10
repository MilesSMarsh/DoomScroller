# Content pipeline run `20261110-021519`

| Stage | Status | Rounds | Check errors per round | Critic must-fix per round |
|---|---|---|---|---|
| weapons | **approved** | 2 | 6 → 0 | 2 → 0 |
| equipment | **approved** | 1 | 0 | 0 |
| recipes | **approved** | 2 | 0 → 0 | 2 → 0 |
| loot | **approved** | 1 | 0 | 0 |
| region | **approved** | 2 | 0 → 0 | 1 → 0 |
| encounters | **approved** | 1 | 0 | 0 |

## Weapons

### Round 1: sent back

Code checks:

- `damage-band` GolembreakerMaul: tier 1 damage 7 outside existing Melee range 2–6
- `damage-band` GolembreakerMaul: tier 2 damage 14 outside existing Melee range 4–12
- `damage-band` GolembreakerMaul: tier 3 damage 26 outside existing Melee range 8–24
- `damage-band` ElderboughLongbow: tier 2 damage 13 outside existing Ranged range 4–12
- `damage-band` IronboltArbalest: tier 1 damage 7 outside existing Ranged range 2–6
- `damage-band` IronboltArbalest: tier 2 damage 13 outside existing Ranged range 4–12

Critic (revise): I'm sending this back for revision because of two major problems. Most of the set works. The starter is right: Hallow Wood Sword is humble, one-handed, from the Green Valley and at the bottom of the band. Forest weapons sit higher than Green Valley ones. Damage per second is close to even within each category and region at both Runed and Exalted. Oblivion only appears on Forest gear. There are no banned words, no hyphens in names, no unapproved made-up words and no satire. The two major problems: Wispfire Recurve is a fire bow sourced from tree people, which pulls in a Volcano-flavoured name, and Dewglass Staff is a Green Valley recipe that needs Frost Shard, a cold-region material. The minor notes cover loot sources that don't fit the item (slimes and predators dropping wands, a golem dropping the maul made to break golems) and a few ability picks that clash with the weapon's fantasy. I haven't repeated the damage-band errors or DPS-spread warnings from the automated checks.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| major | consistency | WispfireRecurve | This is a fire bow ('arrows catch fire') that drops from tree people, so creatures of the old woodland carry fire. 'Wispfire' also points to the Cinder Wisp, a living-fire sprite that belongs with Emberforge and the Volcano, not the Forest. Its pool adds frost (Winter's Mark) and poison (Passive_Venomous), so it has no single elemental identity. | Re-theme it around a Forest element and source. Option 1: an Elemental bow tied to the woods (thorn-sap or storm, for example), renamed to match, with tree people as the source. Option 2: keep the fire, but source it from something that burns, such as a crafted recipe that uses Coal or Sulfur, and rename it so it doesn't borrow the Cinder Wisp. Also cut Winter's Mark or Venomous so the pool reads as one element. |
| major | consistency | DewglassStaff | This Green Valley weapon is crafted from Frost Shard. In the catalog, Frost Shard only comes from cold content (Frozen Ice Shelf, Frostfang Hollow, Frostbound Wraith), which fits the Mountain, not the farmland. Starting-region players would have to wait regions to craft it. The idea of morning dew holding enough chill for a frost-magic staff is also a stretch. | Use a recipe made of Green Valley materials (Wood, Fibers, Healing Herbs, Stone or Iron Ingot). Re-flavour the chill as dew or rain-water magic that suits farmland, or move the frost staff to a later stage. |
| minor | consistency | FieldhandSling | The Rogue Slime is 'a wandering ooze that dissolves whatever it swallows', so an intact cord sling dropping from it contradicts the slime. HedgewitchWand has the same problem. | Source both from goblins, the Wandering Merchant or a Blacksmith recipe (Fibers or Rope plus Wood). |
| minor | consistency | EmberbarkWand | Large predators are animal-like and don't carry wands. The same loot-sense issue applies to ElderboughLongbow and ThornwoodBoarspear, which also list 'Large predators'. | Source crafted weapons from tree people, skeletons or Blacksmith recipes. Keep predator drops to materials, or give a reason in the flavour (gear left by fallen hunters). |
| minor | consistency | GolembreakerMaul | A maul 'made to crack golems' that drops from the Rustbound Golem itself reads backwards. GnarlrootQuarterstaff, a staff of knotted root, also lists the Rustbound Golem (a scrap-iron construct), which contradicts its material. | Make the maul a Blacksmith recipe (Stone plus Aged Timber). Source the quarterstaff from tree people or the Ancient Oak. |
| minor | consistency | ThreshingFlail | Ability_Lacerate is a bleed, which doesn't fit a blunt farm flail. | Replace Lacerate with Riposte, or drop it and keep 4 entries. |
| minor | tone | GoblinKitchenKnife | 'Exalted Goblin Kitchen Knife' reads as a joke at top rarity. Ability_Venomfang ('a deep bite') also doesn't fit a knife. | Use a sincere name built on the approved word (for example 'Grisk Skinning Knife') and swap Venomfang for Sunder or Cleave. |
| minor | clarity | HallowWoodSword | The flavour starts 'A plain blade', which clashes with the rarity word on higher rolls ('Exalted Hallow Wood Sword: A plain blade...'). | Use 'A simple blade' or 'A light blade' instead. |
| minor | consistency | BarnyardThrowingAxe | The weapon scales with Agility, but its passive pool is all Constitution and defence (Hefty, Reinforced, Bulwark). That fits a quick thrown axe poorly. | Swap two of them for Agile, Fleetfoot or Sureshot. |
| minor | naming | ElderboughLongbow | 'Elderbough Longbow' and 'Elderroot Staff' are easy to mix up on a phone card. Both are Forest weapons dropped by tree-linked sources. | Rename one (for example 'Yewheart Longbow'). |
| minor | duplication | EmberbarkWand | It is close to a Forest version of HearthfireRod: a one-handed, fast, Elemental fire weapon that scales with Spirit and leads with Cinderburst, Immolate and Smoldering. | Separate them through scaling or lead abilities. For example, have Emberbark lead with Execute and Crippling, or change its scaling attribute. |
| minor | consistency | CairnSlingstaff | Ability_WintersMark (frost) doesn't fit a stone slingstaff. The same applies to IronboltArbalest and ElderboughLongbow, and the Frostbind and Hoarfrost entries on GnarlrootQuarterstaff are similar. | Where the pool can still reach 5 entries without them, lead with non-frost abilities. Accept the overlap only where the small catalog forces it. |
| minor | tone | ElderrootStaff | 'Humming with old power' is generic and could belong to any fantasy game. GnarlrootQuarterstaff's 'thick with old magic' has the same problem. | Tie both to something specific in the Forest, such as tree people, golems with trees on their backs, or the elder trees themselves. |

*Sounds like the game:* "A farmhand's threshing flail, swung hard enough to scatter a goblin raiding party." It is sincere and plain, rooted in the Green Valley's farms and goblins, and it says what the weapon is for without a wink.

### Round 2: passed

Critic (approve): I'm approving this set. Both major problems from the last review are fixed. Wispfire Recurve is now Stormbough Recurve: a lightning bow from the Forest, sourced from tree people, with no Volcano echo and no frost or poison in its pool. Dewglass Staff is now Rainglass Staff, crafted from Wood and Healing Herbs, both Green Valley materials. Most of the earlier minor notes are also fixed: loot sources now fit the items, Lacerate is off the flail, the knife has a sincere name, the starter says "A simple blade", the throwing axe has Agility passives, the bow names no longer clash, Emberbark leads with Execute and scales with Agility, frost abilities are off the stone and wood ranged weapons, and the Elderroot flavour is now tied to the Forest. The starter is still humble and at the bottom of the band, Forest gear sits above Green Valley gear, damage per second stays close to even within each category, and Oblivion only appears on Forest gear. There are no banned words, no hyphens in names, no unapproved made-up words and no satire. The new notes below are all minor polish: loot sources that don't fit (a wolf drops a bow, a boar drops a blowgun, skeletons drop the lantern that repels them), mixed elements on the Magic weapons (a limit of the small catalog) and one damage-per-second outlier.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | consistency | MeadowlarkShortbow | It drops from the Grey Wolf. A wolf doesn't carry a bow, which is the same loot-sense problem the last review flagged for predators. This probably comes from rewiring the old Enemy_Wolf 'Bow' drop. | Source it from goblins, the Wandering Merchant or a Blacksmith recipe (Wood plus Rope). Leave the Grey Wolf with materials such as Wolf Pelt. |
| minor | consistency | BoarquillBlowgun | It drops whole from the Quillback Boar. The boar would give quills, not a finished reed blowgun. | Make it a Blacksmith recipe (Fibers plus Wood), or source it from goblins. The flavour can still say it fires boar quills. |
| minor | consistency | BarrowlightLantern | The lantern 'drives back the skeletons', yet the skeletons drop it. It also has a 'pale flame', but its pool leads with frost (Frostbind, Hoarfrost). | Source it from the Bonewrought Sentinel's dungeon loot or a recipe (Bone Fragment plus Iron Ingot). Lead the pool with Arcane Bolt and Execute, or describe the flame as a cold light. |
| minor | consistency | MoonmeadowCrook | This is an Arcane shepherd's crook, but its pool includes frost and fire (Frostbind, Cinderburst, Immolate). HedgewitchWand has the same mix. The small Magic catalog limits the options, but three off-element entries out of five is a lot. | Keep at most one elemental entry on each Arcane weapon. Fill the pool with Arcane Bolt, Execute and Warcry (4 entries is acceptable). |
| minor | balance | GolembreakerMaul | It is the flagship Forest two-hander, but it has the lowest Exalted damage per second of the Forest melee weapons (48 × 0.7 ≈ 33.6, against 41.6 for Heartwood Hatchet). Its damage is already at the band cap. | Raise attackSpeed to about 0.8, or lower Heartwood Hatchet slightly, to tighten the Forest melee spread. |
| minor | consistency | RainglassStaff | The recipe is now fixed, but the pool still leads with Frostbind and Hoarfrost (a 'killing frost'), which is strong for 'cool as dawn' rainwater. | Accept this as a catalog limit, or lead with Crippling and Weaken Enemy passives so the staff reads as a soaking slow rather than hard frost. |
| minor | consistency | GnarlrootQuarterstaff | Frostbind is still in the pool of an oak-root staff (a leftover from the last review). | Replace it with Hoarfrost-free options if possible, or accept it as the fourth entry forced by the catalog. |

*Sounds like the game:* "A long harvest scythe that cuts as cleanly through goblins as through wheat." It is plain and sincere, rooted in the Green Valley's farms and goblins, and the tool turned into a weapon tells the player exactly what the item is.

### Final

| Weapon | Region | Type | Damage Plain→Exalted | Speed | Abilities pool | Passives pool | Flavor |
|---|---|---|---|---|---|---|---|
| **Elderroot Staff** (`ElderrootStaff`) | Forest | Magic / Arcane / 2H | [3, 6, 12, 22, 44] | 0.85 | ArcaneBolt, Execute, Oblivion, Warcry, Frostbind | ArcaneFocus, Spiritful, Vicious, DamageUp, Channeled, Hefty | A staff grown from the roots of an elder tree and tended by the tree people of the old woods. |
| **Emberbark Wand** (`EmberbarkWand`) | Forest | Magic / Elemental | [2, 4, 7, 13, 26] | 1.35 | Execute, Cinderburst, Immolate, Warcry, ArcaneBolt | Crippling, Smoldering, Channeled, IncreaseAttackSpeed, Agile, Vicious | A wand of charred bark that flares quick and bright with every flick of the wrist. |
| **Barrowlight Lantern** (`BarrowlightLantern`) | Forest | Magic / Arcane | [2, 5, 10, 17, 34] | 1.05 | Frostbind, Hoarfrost, ArcaneBolt, Execute, Warcry | ArcaneFocus, WeakenEnemy, Crippling, Channeled, Bulwark, Spiritful | A lantern whose pale flame drives back the skeletons of the old woods. |
| **Gnarlroot Quarterstaff** (`GnarlrootQuarterstaff`) | Forest | Magic / Blunt / 2H | [3, 6, 11, 20, 40] | 0.9 | ArcaneBolt, Warcry, Execute, Frostbind | Hefty, Reinforced, Bulwark, WeakenEnemy, DamageUp, Spiritful | A quarterstaff of knotted root, cut from the oldest oaks of the Forest and hard as stone. |
| **Hedgewitch Wand** (`HedgewitchWand`) | Green Valley | Magic / Arcane | [1, 3, 6, 12, 24] | 1.4 | ArcaneBolt, Frostbind, Cinderburst, Warcry, Immolate | ArcaneFocus, Channeled, Spiritful, IncreaseAttackSpeed, DamageUp | A hawthorn wand that crackles with a small, steady arcane charge. |
| **Hearthfire Rod** (`HearthfireRod`) | Green Valley | Magic / Elemental | [1, 3, 7, 13, 26] | 1.2 | Cinderburst, Immolate, ArcaneBolt, Warcry | Smoldering, Spiritful, Vicious, Channeled, Crippling | An iron rod warmed by hearth coals that sparks into flame at a touch. |
| **Rainglass Staff** (`RainglassStaff`) | Green Valley | Magic / Elemental / 2H | [2, 5, 10, 18, 36] | 0.8 | Frostbind, Hoarfrost, ArcaneBolt, Warcry | Crippling, WeakenEnemy, Channeled, Reinforced, Bulwark, Spiritful | A staff tipped with a glass bead of rainwater that never dries, cool as dawn on the fields. |
| **Moonmeadow Crook** (`MoonmeadowCrook`) | Green Valley | Magic / Arcane / 2H | [2, 6, 11, 20, 40] | 0.7 | ArcaneBolt, Warcry, Frostbind, Cinderburst, Immolate | ArcaneFocus, Hefty, DamageUp, Vicious, Reinforced | A shepherd's crook that glows softly under the moon and guides a heavy arcane light. |
| **Golembreaker Maul** (`GolembreakerMaul`) | Forest | Melee / Blunt / 2H | [3, 6, 12, 24, 48] | 0.7 | Cleave, Deathblow, Sunder, Warcry, Execute | Hefty, Thickhide, Ironwilled, Reinforced, DamageUp, Vicious | A great maul of stone and ironwood, made to crack golems at the joints. |
| **Gravewarden Mace** (`GravewardenMace`) | Forest | Melee / Blunt | [3, 5, 10, 19, 38] | 1 | Sunder, Warcry, Riposte, Execute, Cleave | Thickhide, Reinforced, Bulwark, WeakenEnemy, Hefty | A bone-breaking mace carried by those who clear the old skeletons from the woods. |
| **Thornwood Boarspear** (`ThornwoodBoarspear`) | Forest | Melee / Pierce / 2H | [3, 6, 11, 21, 42] | 0.9 | Lacerate, Riposte, VenomStrike, Execute, Sunder | Agile, Crippling, Vicious, WeakenEnemy, Venomous, IncreaseAttackSpeed | A thornwood spear with a broad head, braced against the charge of large predators. |
| **Heartwood Hatchet** (`HeartwoodHatchet`) | Forest | Melee / Slash | [2, 4, 8, 16, 32] | 1.3 | Slash, Lacerate, Cleave, Riposte, Sunder | Agile, IncreaseAttackSpeed, Vicious, DamageUp, Crippling | A hatchet of heartwood that bites quickly into trunk and tree person alike. |
| **Hallow Wood Sword** (`HallowWoodSword`) ★ starter | Green Valley | Melee / Slash | [1, 3, 6, 12, 24] | 1.2 | Slash, Cleave, Riposte, Warcry | DamageUp, Bulwark, Agile, Hefty, IncreaseAttackSpeed | A simple blade carved from hallow wood, light enough for any new adventurer. |
| **Threshing Flail** (`ThreshingFlail`) | Green Valley | Melee / Blunt | [2, 5, 9, 17, 34] | 0.9 | Cleave, Sunder, Warcry, Slash, Riposte | Hefty, Thickhide, Bulwark, DamageUp, Ironwilled | A farmhand's threshing flail, swung hard enough to scatter a goblin raiding party. |
| **Grisk Skinning Knife** (`GriskSkinningKnife`) | Green Valley | Melee / Pierce | [1, 2, 5, 10, 20] | 1.5 | Lacerate, VenomStrike, Slash, Riposte, Sunder | Agile, IncreaseAttackSpeed, Venomous, Crippling, Vicious | A goblin's skinning knife, ground sharp and quick in the hand. |
| **Wheatcutter Scythe** (`WheatcutterScythe`) | Green Valley | Melee / Slash / 2H | [2, 6, 11, 20, 40] | 0.7 | Cleave, Lacerate, Sunder, Warcry, Slash | Vicious, Hefty, DamageUp, Ironwilled, Reinforced | A long harvest scythe that cuts as cleanly through goblins as through wheat. |
| **Yewbough Longbow** (`YewboughLongbow`) | Forest | Ranged / Pierce / 2H | [3, 6, 12, 23, 45] | 0.8 | PiercingShot, Execute, Volley, Hamstring, VenomStrike | Sureshot, Vicious, Agile, DamageUp, Fleetfoot, Crippling | A tall longbow of yew bough that reaches between the great trunks. |
| **Ironbolt Arbalest** (`IronboltArbalest`) | Forest | Ranged / Pierce / 2H | [3, 6, 12, 24, 48] | 0.75 | PiercingShot, Execute, Hamstring, Volley | Sureshot, Vicious, WeakenEnemy, Hefty, Reinforced, DamageUp | A heavy iron-braced crossbow that punches bolts through plate. |
| **Stormbough Recurve** (`StormboughRecurve`) | Forest | Ranged / Elemental / 2H | [2, 5, 10, 18, 36] | 1.1 | Volley, PiercingShot, Hamstring, Execute | Fleetfoot, Sureshot, IncreaseAttackSpeed, Agile, Vicious | A recurve bow cut from a lightning-struck bough, its arrows crackling as they leave the string. |
| **Cairn Slingstaff** (`CairnSlingstaff`) | Forest | Ranged / Blunt / 2H | [3, 5, 10, 19, 38] | 0.95 | PiercingShot, Hamstring, Execute, Volley | Hefty, Reinforced, Bulwark, WeakenEnemy, Vicious, Crippling | A long slingstaff that throws cairn stones with a golem's weight. |
| **Fieldhand Sling** (`FieldhandSling`) | Green Valley | Ranged / Blunt | [1, 3, 6, 11, 22] | 1.3 | Hamstring, PiercingShot, Volley, VenomStrike | Fleetfoot, Sureshot, Agile, DamageUp, IncreaseAttackSpeed | A cord sling that hurls river stones clear across the fields. |
| **Meadowlark Shortbow** (`MeadowlarkShortbow`) | Green Valley | Ranged / Pierce / 2H | [2, 4, 8, 15, 30] | 1.1 | PiercingShot, Volley, Hamstring, VenomStrike | Sureshot, Fleetfoot, Agile, Vicious, Crippling, Venomous | A supple shortbow strung for hunting along the meadow's edge. |
| **Barnyard Throwing Axe** (`BarnyardThrowingAxe`) | Green Valley | Ranged / Slash | [1, 3, 6, 12, 24] | 1.4 | Hamstring, Volley, PiercingShot, VenomStrike | Agile, Fleetfoot, Sureshot, WeakenEnemy, DamageUp | A balanced farm axe made for throwing at anything that wanders too close to the barn. |
| **Boarquill Blowgun** (`BoarquillBlowgun`) | Green Valley | Ranged / Pierce | [1, 2, 5, 10, 20] | 1.5 | VenomStrike, Hamstring, Volley, PiercingShot | Venomous, Crippling, IncreaseAttackSpeed, Fleetfoot, Agile, WeakenEnemy | A reed blowgun that fires hardened boar quills. |

## Equipment

### Round 1: passed

Critic (approve): The full set of 12 follows the rules: two pieces per slot, split by region; every power value rises tier by tier and stays in the band; each Forest piece is stronger than its Green Valley partner; Chest pieces are stronger than Boots; every passive pool has at least four ids; names have no hyphens and are 35 characters or fewer; all flavour lines are under 120 characters. The tone is sincere and grounded in the catalog (Quillback Boar, golems, the elder tree, valley farms). Remaining notes are polish: one name contains "post", there are some similar names across items, two lines of flavour text don't fit their item well, and a few damage passives sit on armour.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | tone | GatepostBuckler | The name and flavour text contain "post" (inside "gatepost"), one of the brief's banned feed words. The automated word check passed it, but on a card title it can read as a wink at social media, outside the satire budget. | Rename to something like "Barngate Buckler" or "Fencerail Buckler" and update the flavour text to match (e.g. "cut from an old farm gate"). |
| minor | naming | MossweaveGreaves | Mossweave Greaves and Mossback Shield are both Forest pieces starting with "Moss", which makes them easy to confuse on a phone card. | Rename one, e.g. Mossweave Greaves → "Fernweave Greaves" (or another distinct Forest word). |
| minor | clarity | MossweaveGreaves | "Greaves" means rigid shin plates, but the flavour text describes leg guards woven from moss and cord. | Call them "Leggings" or "Leg Wraps", or add bark or wood plates to the flavour text. |
| minor | duplication | MossbackShield | "Stone and ironwood" repeats the Golembreaker Maul's flavour text word for word, and ironwood is not a material in the catalog. | Use catalog materials and fresh wording, e.g. "A broad shield of stone slabs and aged timber, furred with moss…". |
| minor | duplication | HedgerowCharm | The woven hawthorn and the "Hedge…" name echo the Hedgewitch Wand ("A hawthorn wand"), another Green Valley item. | Use a different plant or object (e.g. rowan berries, a corn dolly) and a name that doesn't start with "Hedge". |
| minor | consistency | RootstrideBoots | The task asks for defensive passives on sturdy armour, but Vicious (flat damage) sits on boots. Hayfield Helm (Damage Up, Hefty) and Antlerbark Helm (Vicious) lean on damage passives too. | Swap Vicious on Rootstride Boots for Bulwark or Thickhide. Consider replacing Damage Up on Hayfield Helm with Weaken Enemy or Thickhide. |

*Sounds like the game:* "A tough jerkin of boar hide, stitched to turn aside tusks and quills." It is plain and sincere, and ties the gear to the Quillback Boar already in the valley, so the armour feels earned from this world rather than generic.

### Final

| Piece | Region | Slot | Power Plain→Exalted | Passives pool | Flavor |
|---|---|---|---|---|---|
| **Hayfield Helm** (`HayfieldHelm`) | Green Valley | Armor / Head | [6, 10, 16, 25, 38] | Bulwark, Reinforced, Hefty, DamageUp | A dented iron cap stuffed with straw padding, worn by farmhands who turn out to guard the harvest. |
| **Antlerbark Helm** (`AntlerbarkHelm`) | Forest | Armor / Head | [8, 14, 22, 34, 50] | Reinforced, Bulwark, WeakenEnemy, Vicious | A helm of hardened bark crowned with shed antlers from the deep woods. |
| **Boarhide Jerkin** (`BoarhideJerkin`) | Green Valley | Armor / Chest | [8, 13, 20, 31, 46] | Bulwark, Reinforced, Hefty, WeakenEnemy | A tough jerkin of boar hide, stitched to turn aside tusks and quills. |
| **Oakheart Cuirass** (`OakheartCuirass`) | Forest | Armor / Chest | [10, 17, 26, 40, 60] | Reinforced, Bulwark, Hefty, WeakenEnemy, DamageUp | A breastplate of seasoned oak bound in iron, as sturdy as the trees it was cut from. |
| **Drover Leggings** (`DroverLeggings`) | Green Valley | Armor / Legs | [7, 11, 17, 26, 40] | Bulwark, Agile, Hefty, IncreaseAttackSpeed | Hard-wearing leggings of waxed canvas, made for long days herding cattle along the valley roads. |
| **Mossweave Greaves** (`MossweaveGreaves`) | Forest | Armor / Legs | [9, 15, 23, 35, 52] | Reinforced, Agile, Bulwark, Crippling | Leg guards woven from living moss and cord, quiet among the roots of the old woods. |
| **Furrow Boots** (`FurrowBoots`) | Green Valley | Armor / Boots | [5, 9, 14, 22, 33] | Agile, IncreaseAttackSpeed, Bulwark, DamageUp | Mud-caked leather boots that have walked every furrow of the valley farms. |
| **Rootstride Boots** (`RootstrideBoots`) | Forest | Armor / Boots | [7, 12, 19, 29, 44] | Agile, IncreaseAttackSpeed, Reinforced, Vicious | Boots with gnarled root soles that grip the forest floor over stone and fallen timber. |
| **Gatepost Buckler** (`GatepostBuckler`) | Green Valley | Shield / LeftHand | [7, 12, 18, 28, 42] | Bulwark, Reinforced, Hefty, WeakenEnemy | A round buckler cut from an old farm gatepost, still bearing its iron hinge. |
| **Mossback Shield** (`MossbackShield`) | Forest | Shield / LeftHand | [9, 16, 24, 37, 56] | Reinforced, Bulwark, Hefty, WeakenEnemy, Crippling | A broad shield of stone and ironwood, furred with moss and heavy enough to stop a golem's fist. |
| **Hedgerow Charm** (`HedgerowCharm`) | Green Valley | Talisman / Talisman1 | [6, 10, 16, 24, 36] | ArcaneFocus, Spiritful, Channeled, DamageUp | A small charm of woven hawthorn twigs, carried for luck along the meadow roads. |
| **Heartsap Pendant** (`HeartsapPendant`) | Forest | Talisman / Talisman1 | [8, 14, 21, 33, 48] | ArcaneFocus, Spiritful, Channeled, Vicious, WeakenEnemy | A pendant of amber hardened from the sap of an elder tree, warm to the touch and faintly glowing. |

## Recipes

### Round 1: sent back

Critic (revise): The structure is solid. There is exactly one recipe for each of the 34 non-starter items, and every rarity fits its region (Green Valley at Plain or Tempered, Forest at Tempered, Runed or Relic). Every gold cost and ingredient total is inside recipeBand. No prerequisite crafts at a higher rarity than the recipe that needs it, there are no cycles, and the tree climbs from Green Valley basics into Forest gear. Most ingredients match the item's own text and the weapons-stage notes (Golembreaker Maul, Cairn Slingstaff, Yewbough Longbow, Antlerbark Helm). Two Green Valley recipes are locked behind materials from later regions, which breaks the region-by-region progression this recipe tree is meant to build: Rainglass Staff needs a Frost Shard (ice content) and Boarquill Blowgun needs a Venom Sac (Widow Spider). There are also a few small mismatches between materials and item text, and the catalog has no way to smelt Iron Ingot.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| major | consistency | Recipe_RainglassStaff | This Green Valley Tempered staff needs 1 Frost Shard. Frost Shard only comes from ice content (Frozen Ice Shelf, Frostfang Hollow, Frostbound Wraith), which fits the Mountain's ice golems and dire wolves, not the valley. A region 1 weapon can't be crafted until much later, which breaks the climb from Green Valley basics. It also goes against the approved weapons-stage note ('Crafted at the Blacksmith from Wood and Healing Herbs') and the item's own fantasy (a bead of rainwater, not ice). | Remove Frost Shard. Use Wood 4 + Healing Herbs 3 (7 total), or add 1 Stone or Arcane Gem for the glass bead. Keep the total at 5–8 and the gold at 25–28. |
| major | consistency | Recipe_BoarquillBlowgun | This Green Valley Plain blowgun needs 1 Venom Sac. Venom Sac only drops from the Widow Spider, the same creature as Spider Silk, which the task lists as a Forest material. An entry-level region 1 weapon is locked behind a later region. The item's text is about hardened boar quills, not venom. | Replace Venom Sac with Green Valley materials that match the quill and reed fantasy. For example: Wood 3 + Fibers 3 + Rope 1 (add the Recipe_Rope prerequisite), or Wood 4 + Fibers 3. Quillback Boar already drops Wood and Fibers, which ties the recipe to the item's source. |
| minor | balance | Recipe_BarnyardThrowingAxe | The catalog has no Recipe_IronIngot, so Iron Ingot only drops from Rustbound Golem, Bonewrought Sentinel and Bonewrought Depths, which look like Forest or ancient content. That makes several Green Valley Plain and Tempered recipes (Barnyard Throwing Axe, Gatepost Buckler, Hearthfire Rod, Wheatcutter Scythe, Boarhide Jerkin) depend on drops from outside the region. Hearthfire Rod and Wheatcutter Scythe have Iron Ingot fixed by their approved notes; Barnyard Throwing Axe and Gatepost Buckler were the designer's choice. | In Recipe_BarnyardThrowingAxe and Recipe_GatepostBuckler, use Iron Ore instead of Iron Ingot (the designer already does this in Recipe_HayfieldHelm and Recipe_GriskSkinningKnife). Separately, tell the creative director that the material recipe tree needs a Recipe_IronIngot (Iron Ore + Coal) so Green Valley iron gear can actually be crafted in Green Valley. |
| minor | consistency | Recipe_BoarhideJerkin | The item is described as 'a tough jerkin of boar hide', but the recipe uses 4 Wolf Pelt as its main material. No boar hide material exists, so this is a catalog limit, but the hide doesn't match the item's text. | Keep Wolf Pelt as the closest hide for now, but flag that a Boar Hide material is needed (dropped by Quillback Boar). Or drop the pelt count to 2 and make up the total with Fibers or Rope so the recipe reads less like a wolf-pelt jerkin. |
| minor | consistency | Recipe_HeartsapPendant | The item is 'a pendant of amber hardened from the sap of an elder tree', but the recipe uses no wood at all (Arcane Gem, Healing Herbs, Silver Ingot). | Replace Healing Herbs with Aged Timber 2 so the sap and tree source shows in the recipe. The total stays at 5. |

*Sounds like the game:* Recipe_GolembreakerMaul (3 Stone + 3 Aged Timber) is the best fit. It builds exactly what its card promises, "a great maul of stone and ironwood, made to crack golems at the joints", and needs a Green Valley scythe first, so the crafting tree reads like the hero's own road from the farms into the old woods.

### Round 2: passed

Critic (approve): All five issues from the last review are fixed. Rainglass Staff is now Wood 4 + Healing Herbs 3, which matches its approved note and no longer needs Frost Shard. Boarquill Blowgun is now Wood 3 + Fibers 3 + Rope 1 (with the Rope prerequisite), so Venom Sac is gone. Barnyard Throwing Axe and Gatepost Buckler now use Iron Ore. Boarhide Jerkin is less pelt-heavy, and Heartsap Pendant now uses Aged Timber. Every recipe still sits inside recipeBand for its rarity. Region rarities are correct, and no prerequisite crafts at a higher rarity than the recipe that needs it. The tree climbs cleanly from Green Valley basics into Forest gear. One catalog-level note remains: there is no way to smelt Iron Ingot.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | balance | Recipe_HearthfireRod | The catalog has no Recipe_IronIngot, so Iron Ingot only drops from Forest and ancient content (Rustbound Golem, Bonewrought Sentinel, Bonewrought Depths). Green Valley recipes Recipe_HearthfireRod and Recipe_WheatcutterScythe still need it. Their approved weapons notes fix Iron Ingot as an ingredient, so this is a catalog gap, not a design error. | Tell the creative director to add a Plain material recipe, Recipe_IronIngot (for example Iron Ore 3 + Coal 1 for 10 gold, the same as Silver Ingot), and add it as a prerequisite on Recipe_HearthfireRod and Recipe_WheatcutterScythe so Green Valley iron gear can be crafted in Green Valley. |

*Sounds like the game:* The best fit is Recipe_GolembreakerMaul (3 Stone + 3 Aged Timber, after the Wheatcutter Scythe). It builds exactly the "great maul of stone and ironwood" its card promises, and the tree reads like the hero's own road from the valley farms into the old woods.

### Final

| Recipe | Crafts | Gold | Ingredients | Needs first |
|---|---|---|---|---|
| `Recipe_ThreshingFlail` | Plain ThreshingFlail | 12 | 3 Wood, 2 Rope, 2 Stone | Recipe_Rope |
| `Recipe_GriskSkinningKnife` | Plain GriskSkinningKnife | 10 | 3 IronOre, 1 Wood, 1 WolfPelt | — |
| `Recipe_WheatcutterScythe` | Tempered WheatcutterScythe | 26 | 3 Wood, 3 IronIngot | Recipe_ThreshingFlail |
| `Recipe_FieldhandSling` | Plain FieldhandSling | 8 | 2 Rope, 2 Stone | Recipe_Rope |
| `Recipe_MeadowlarkShortbow` | Tempered MeadowlarkShortbow | 25 | 4 Wood, 2 Rope, 1 WolfPelt | Recipe_FieldhandSling |
| `Recipe_BarnyardThrowingAxe` | Plain BarnyardThrowingAxe | 14 | 2 Wood, 2 IronOre, 1 Rope | Recipe_Rope |
| `Recipe_BoarquillBlowgun` | Plain BoarquillBlowgun | 12 | 3 Wood, 3 Fibers, 1 Rope | Recipe_Rope |
| `Recipe_HedgewitchWand` | Plain HedgewitchWand | 16 | 3 Wood, 2 ArcaneGem | — |
| `Recipe_HearthfireRod` | Plain HearthfireRod | 16 | 3 Coal, 2 IronIngot | — |
| `Recipe_RainglassStaff` | Tempered RainglassStaff | 27 | 4 Wood, 3 HealingHerbs | Recipe_HedgewitchWand |
| `Recipe_MoonmeadowCrook` | Tempered MoonmeadowCrook | 28 | 3 Wood, 3 ArcaneGem, 1 SilverIngot | Recipe_HedgewitchWand, Recipe_SilverIngot |
| `Recipe_HayfieldHelm` | Plain HayfieldHelm | 12 | 3 IronOre, 3 Fibers | — |
| `Recipe_BoarhideJerkin` | Tempered BoarhideJerkin | 25 | 2 WolfPelt, 3 Rope, 1 IronOre | Recipe_Rope |
| `Recipe_DroverLeggings` | Plain DroverLeggings | 10 | 2 WolfPelt, 5 Fibers | — |
| `Recipe_FurrowBoots` | Plain FurrowBoots | 8 | 2 WolfPelt, 3 Fibers | — |
| `Recipe_GatepostBuckler` | Plain GatepostBuckler | 14 | 4 Wood, 2 IronOre | — |
| `Recipe_HedgerowCharm` | Plain HedgerowCharm | 10 | 3 Fibers, 2 HealingHerbs, 1 ArcaneGem | — |
| `Recipe_GolembreakerMaul` | Runed GolembreakerMaul | 32 | 3 Stone, 3 AgedTimber | Recipe_WheatcutterScythe |
| `Recipe_GravewardenMace` | Tempered GravewardenMace | 26 | 3 BoneFragment, 2 IronIngot | Recipe_ThreshingFlail |
| `Recipe_ThornwoodBoarspear` | Runed ThornwoodBoarspear | 30 | 3 AgedTimber, 2 BoneFragment | Recipe_GriskSkinningKnife |
| `Recipe_HeartwoodHatchet` | Tempered HeartwoodHatchet | 27 | 3 AgedTimber, 2 IronIngot | Recipe_WheatcutterScythe |
| `Recipe_YewboughLongbow` | Runed YewboughLongbow | 33 | 3 AgedTimber, 2 Rope | Recipe_MeadowlarkShortbow, Recipe_Rope |
| `Recipe_IronboltArbalest` | Relic IronboltArbalest | 60 | 3 SteelIngot, 2 AgedTimber, 2 Rope | Recipe_SteelIngot, Recipe_YewboughLongbow |
| `Recipe_StormboughRecurve` | Relic StormboughRecurve | 65 | 3 AgedTimber, 2 ArcaneGem, 2 SpiderSilk | Recipe_YewboughLongbow |
| `Recipe_CairnSlingstaff` | Runed CairnSlingstaff | 30 | 3 Stone, 2 Rope | Recipe_FieldhandSling |
| `Recipe_ElderrootStaff` | Relic ElderrootStaff | 70 | 3 AgedTimber, 3 ArcaneGem, 1 HealingHerbs | Recipe_MoonmeadowCrook |
| `Recipe_EmberbarkWand` | Tempered EmberbarkWand | 26 | 3 Coal, 3 AgedTimber | Recipe_HearthfireRod |
| `Recipe_BarrowlightLantern` | Runed BarrowlightLantern | 35 | 2 BoneFragment, 2 ArcaneGem, 1 SteelIngot | Recipe_HedgewitchWand, Recipe_SteelIngot |
| `Recipe_GnarlrootQuarterstaff` | Runed GnarlrootQuarterstaff | 32 | 4 AgedTimber, 2 HealingHerbs | Recipe_RainglassStaff |
| `Recipe_AntlerbarkHelm` | Runed AntlerbarkHelm | 31 | 2 AgedTimber, 2 BoneFragment, 1 SteelIngot | Recipe_HayfieldHelm, Recipe_SteelIngot |
| `Recipe_OakheartCuirass` | Relic OakheartCuirass | 75 | 4 AgedTimber, 2 SteelIngot, 1 Rope | Recipe_BoarhideJerkin, Recipe_SteelIngot |
| `Recipe_MossweaveGreaves` | Tempered MossweaveGreaves | 27 | 3 SpiderSilk, 2 AgedTimber, 1 HealingHerbs | Recipe_DroverLeggings |
| `Recipe_RootstrideBoots` | Runed RootstrideBoots | 30 | 3 AgedTimber, 2 SpiderSilk | Recipe_FurrowBoots |
| `Recipe_MossbackShield` | Relic MossbackShield | 60 | 3 Stone, 3 AgedTimber, 1 SteelIngot | Recipe_GatepostBuckler, Recipe_SteelIngot |
| `Recipe_HeartsapPendant` | Runed HeartsapPendant | 34 | 2 ArcaneGem, 2 AgedTimber, 1 SilverIngot | Recipe_HedgerowCharm, Recipe_SilverIngot |

## Loot

### Round 1: passed

Critic (approve): Loot rewire keeps each card's spirit (the fire scepter becomes two fire casters, the bone cleaver becomes the Gravewarden Mace, the grimoire becomes the Barrowlight Lantern, the wolf's bow becomes the Meadowlark Shortbow). Difficulty tiers match: Forest gear on the hardest cards, Green Valley gear on the Grey Wolf. It agrees with the dropsFrom notes approved in the weapons stage. Item counts are all within 1 to 4. The only issues are weak lore fits for a few items, which are optional polish.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | consistency | Dungeon_HollowCrownCrypt | ElderrootStaff's own flavour text says it is tended by the tree people of the old woods (dropsFrom: tree people), so it is an odd find in a necrotic crypt sealed beneath Bonewrought Depths. The crypt already has BarrowlightLantern as its arcane weapon. | Remove ElderrootStaff, or swap in a weapon not tied to the tree people (e.g. GolembreakerMaul), keeping 4 items at most. |
| minor | consistency | Enemy_BonewroughtSentinel | GolembreakerMaul is 'made to crack golems at the joints', which is a weak fit for a fused-bone guardian. GravewardenMace and AntlerbarkHelm already suit it. | Drop GolembreakerMaul, or say in the reason why a bone guardian of the ancient race would hold it (e.g. tools found in ancient-race ruins). |
| minor | consistency | Enemy_HollowCrownRevenant | MossbackShield (moss-covered, 'heavy enough to stop a golem's fist') doesn't fit a crowned horror risen from the deepest crypt. | Replace it with armour that fits a crowned undead warrior better, e.g. OakheartCuirass or AntlerbarkHelm. |
| minor | consistency | Dungeon_WidowsThicket | GriskSkinningKnife is named and described as a goblin's knife, which reads oddly as spider-lair loot. Two Green Valley weapons are also on the low side for a mid-level dungeon (140 XP). | Swap GriskSkinningKnife for ThornwoodBoarspear (Forest-tier pierce weapon with Venom Strike and Venomous in its pools), which also keeps the venom theme of the old fang dagger. |
| minor | balance | Enemy_Wolf | Out of scope for this draft: the existing card's rarity odds are 0/0/0/0/100, so the easiest card always drops Exalted MeadowlarkShortbow or DroverLeggings. | Flag for the balance pass: give the Grey Wolf odds weighted towards Plain, like other starter enemies (e.g. 70/20/6/3/1). |

*Sounds like the game:* "A lantern whose pale flame drives back the skeletons of the old woods." (Barrowlight Lantern, chosen for the crypt and the Revenant): plain, sincere fantasy with real danger and a clear purpose, and choosing it as an undead drop makes the loot tell a story.

### Final

| Card | New weapon drops | New equipment drops | Why |
|---|---|---|---|
| `Dungeon_EmberforgeRuins` | EmberbarkWand, HearthfireRod | — | Fire scepter replaced by the two elemental fire casters; mid-tier ruin mixes Forest and Green Valley gear. |
| `Dungeon_HollowCrownCrypt` | GravewardenMace, BarrowlightLantern, ElderrootStaff | OakheartCuirass | Hardest dungeon gets top Forest gear: bone-breaking mace for the cleaver, arcane Relic staff and lantern for the grimoire, plus Relic chest armor. |
| `Dungeon_WidowsThicket` | GriskSkinningKnife, BoarquillBlowgun | MossweaveGreaves | Venom-friendly pierce weapons replace the fang dagger; Mossweave Greaves tie to the spider silk the lair holds. |
| `Enemy_BonewroughtSentinel` | GravewardenMace, GolembreakerMaul | AntlerbarkHelm | Blunt weapons replace the cleaver and war hammer, and a Forest helm replaces the leather helmet; both weapons suit a bone guardian. |
| `Enemy_HollowCrownRevenant` | GravewardenMace, BarrowlightLantern | MossbackShield | Toughest enemy: undead-slaying Forest gear, with a mace for the cleaver and a lantern for the grimoire, plus a Relic shield. |
| `Enemy_Wolf` | MeadowlarkShortbow | DroverLeggings | Easiest card gets Green Valley gear: a hunting shortbow replaces the bow, and leggings fit a hunter's pelt. |

## Region

### Round 1: sent back

Critic (revise): This is a strong draft that matches the World table. The region is Green Valley, the boss is a massive roc, and the key item is the Roc Pinion. The difficulty range (20–100 HP, 5–11 damage, 50–140 XP) sits in the easy part of the current power band. Every fight card listed falls inside that range. The boss sits above the range, keeps its damage between attempts and regains HP slowly. The text is sincere, uses no "you" and no banned words, and has no satire on screen. There is one major problem: the victory line says a pinion drifts down, but the GDD only drops the key item when the boss is beaten above 50% max HP. A player who wins below that line would be told they got an item they didn't get. The rest is polish: an Elemental weakness that doesn't fit a "Storm" roc, loot that makes little sense from a bird, a size mismatch between the card and the key item, and one clumsy line in the region description.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| major | gdd-rule | Boss_UlmarStormRoc.victoryLine | "The great roc falls, and a single pinion drifts down to the grass." This line plays on every win, but the GDD only drops the key item when the boss is beaten above 50% max HP. A player who wins below that line is told a pinion fell, and then no Roc Pinion appears. | Don't mention the pinion in the victory line, e.g. "The great roc crashes into the tall grass, its storm spent." The description already hints at the key item, so the victory line doesn't need to. |
| minor | consistency | Boss_UlmarStormRoc.damageTypeModifiers | A boss named for storms takes extra Elemental damage (1.25×), and this run's lightning bow (Stormbough Recurve) is Elemental. That clashes with the boss's name. Pierce 1.5 (arrows bring down a bird) is the right signature weakness. | Drop the Elemental weakness or make it a mild resistance (0.75–0.9). Keep Pierce 1.5 as the clear answer. If the boss needs a second weakness, Slash fits better (cutting through feathers). |
| minor | consistency | Boss_UlmarStormRoc.lootMaterials | A roc dropping Silver Ingot, Iron Ingot and Rope doesn't make sense: those are smelted and crafted goods, not things a bird would carry. The weapons it drops (throwing axe, threshing flail) also need a story to explain them. | Frame the loot as the hoard in its nest and say so in the designer note. Otherwise, lean on raw materials that could plausibly end up there, such as Silver Ore, Iron Ore, Healing Herbs and Wood, and keep the ingots for crafting. |
| minor | consistency | Boss_UlmarStormRoc.keyItem | The card says the pinion is "longer than a barn beam", but the key item calls it "long as a spear". These are two different sizes for the same feather. | Use one image in both places, e.g. change the key item to "A pinion from the Storm Roc, long as a barn beam and light as air." |
| minor | clarity | Region_GreenValley.description | "...prowl the edges of the fields, and the fields are worth crossing." The word "fields" repeats and the clause is vague; it doesn't say why the fields are worth crossing. | Make it concrete, e.g. "Slimes, wolves and goblin raiders prowl the edges of the fields, but old coin and good timber reward anyone who looks." |
| minor | consistency | Region_GreenValley.existingCards | Enemy_MarshLeech is named for marshland, which belongs to the Swamp region in the brief, yet it is placed in the farmland valley. Its stats fit only here, so the card's name is what's out of place. | Keep the card in region 1 but plan a rename for the valley, e.g. "Millpond Leech" or "Ditch Leech", with the description changed to match. |
| minor | balance | Region_GreenValley.existingCards | Enemy_Wolf, the first fight new players will see in region 1, has an existing loot table of 100% Exalted and an attack speed of 0.1. This is a catalog problem, outside this draft's scope, but it will undercut the region's curve. | Send this to the Combat and Economy Balance Analysts to retune Enemy_Wolf's loot table (Plain-heavy) and attack speed before the region ships. |

*Sounds like the game:* "A roc so vast its wings shade whole farms, with one great pinion longer than a barn beam." It is plain and earnest, rooted in the farmland, and it hints at the key item the way the brief asks, with no wink and no gloom.

### Round 2: passed

Critic (approve): This revision fixes the one major issue from the last review. The victory line no longer mentions the pinion, so it can't promise a Roc Pinion that only drops when the boss is beaten above 50% max HP. The previous minor issues are also fixed: the Elemental weakness is gone and Pierce 1.5 is the clear answer, the boss now drops raw materials, the pinion is a barn beam in both places, and the region description says what the fields reward. The Marsh Leech rename and Grey Wolf retune are noted for later. Every listed fight card is inside the range (20–100 HP, 5–11 damage, 50–140 XP). The boss sits clearly above it, keeps its damage between attempts and regains HP slowly. The name follows the rules and the text is sincere with no banned words. Three minor points remain: the boss drops gear without a nest-hoard explanation, production notes are stored in hiddenSatire, and the Widow's Thicket drops mostly Forest gear in region 1.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | consistency | Boss_UlmarStormRoc.lootWeapons | This is still open from the last review. A roc drops a throwing axe, a threshing flail, a helm, a buckler and a charm, and nothing in the draft explains why a bird would have farm tools and armor. | Add a designer note that this is the hoard in the roc's nest, taken from farms it raided. Alternatively, cut the list to the weapons that fit best (the shortbow and the throwing axe). |
| minor | clarity | Region_GreenValley.hiddenSatire | The hiddenSatire field also holds production to-dos (renaming Marsh Leech, retuning Grey Wolf). That field is meant only for the satire note, so the to-dos may get lost or confuse whoever reads it. | Keep hiddenSatire to the skimming satire alone, and move the rename and retune notes to the review hand-off or the cardMix designer notes. |
| minor | consistency | Region_GreenValley.existingCards | Dungeon_WidowsThicket sits in region 1, but its approved loot includes Mossweave Greaves (Forest gear), and giant spiders read more like the woodland. This is acceptable but slightly off the valley's theme. | No change is needed now. When region 2 is designed, consider moving the Thicket to the Forest or swapping its Greaves for valley gear. |

*Sounds like the game:* "A roc so vast its wings shade whole farms, with one great pinion longer than a barn beam." It is plain and earnest, rooted in the valley's farmland, and it hints at the key item exactly as the brief asks, with no wink and no gloom.

### Final

**Green Valley** — *Rolling fields and farm roads where every adventurer begins.*

Rolling grassland, patchwork farms and winding roads, with villages tucked between the hills. Slimes, wolves and goblin raiders prowl the edges of the fields, but old coin and good timber reward anyone who looks. High above it all, a roc wheels over the valley.

- Difficulty: HP [20, 100], damage [5, 11], XP [50, 140]
- Cards: Enemy_Wolf, Enemy_RogueSlime, Enemy_QuillbackBoar, Enemy_MarshLeech, Enemy_WidowSpider, Ambush_Bandit, Ambush_HighwayCutthroat, Dungeon_WidowsThicket, Resource_SturdyTree, Resource_LargeThicket, Resource_LargeBoulder, Resource_IronVein, Resource_SilverVein, Resource_WebbedHollow, Treasure_GlitteringChest, Merchant_WanderingTrader, Tavern_TheTavern
- Mix: Mostly Combat against slimes, boars, wolves, leeches and spiders, with a smaller share of Resource nodes and the Glittering Chest. Wandering Merchants and the Widow's Thicket appear only occasionally, and the two bandit Ambushes are rare.
- Hidden satire (designer note): Designer note only: the valley mocks the habit of skimming past the easy, quiet cards. Its safe-looking farmland hides the same rewards and ambushes as anywhere else, so the player who rushes past without looking finds fewer resources and meets more forced fights. Also plan a valley rename for Enemy_MarshLeech (e.g. Ditch Leech) and a loot and attack speed retune of Enemy_Wolf for the balance analysts.

**Solo boss: Ulmar the Storm Roc, Storm Roc** (`Boss_UlmarStormRoc`)

> A roc so vast its wings shade whole farms, with one great pinion longer than a barn beam.

- Intro: “A shriek rolls across the valley as vast wings shut out the sun.”
- Victory: “The great roc crashes into the tall grass, its storm spent.”
- Defeat: “The roc wheels away on the wind, wings beating strong.”
- HP 320 · damage 13 · speed 0.9 · regains 10%/h · XP 300 · gold 120
- Weaknesses: Pierce ×1.5, Slash ×1.15, Blunt ×0.75
- Loot: MeadowlarkShortbow, BarnyardThrowingAxe, ThreshingFlail, HayfieldHelm, GatepostBuckler, HedgerowCharm, SilverOre, IronOre, HealingHerbs, Wood (rarity [10, 30, 35, 20, 5])
- Key item: **Roc Pinion**: A pinion from the Storm Roc, long as a barn beam and light as air. It opens the road into the Forest.

## Encounters

### Round 1: passed

Critic (approve): All five cards obey the GDD. Enemy/Ambush stats sit inside Green Valley's range (HP 20–100, dmg 5–11, XP 50–140), attack speeds are 0.5–1.2, and the Resource never fights back, pays 0 XP/0 gold and has rising thresholds. The mix is 3 Enemy, 1 Ambush and 1 Resource. Naming and tone follow the brief: Grisk is approved, there are no hyphens, banned words or 'you', and there is no satire, which is within limits. Drops line up with the approved weapons' dropsFrom notes (Grisk Skinning Knife from the goblin; Threshing Flail and Barnyard Throwing Axe from the raiding party). Only minor polish remains: a badger dropping Wolf Pelt, the raiding party's spears versus its farm-tool loot, and the new slime's stats sitting close to Rogue Slime's.

| Severity | Category | Target | Problem | Fix |
|---|---|---|---|---|
| minor | consistency | Enemy_BurrowBadger | A badger drops Wolf Pelt, which makes no sense for the creature that drops it. | Replace WolfPelt with a material a badger's den could plausibly yield (e.g. HealingHerbs or Stone), or keep Fibers alone. |
| minor | consistency | Ambush_GoblinRaidingParty | The card text says 'Spears rattle', but the party drops a blunt flail and a throwing axe, with no spear or Pierce weapon. | Either add a Pierce weapon to the loot, or reword so the goblins carry stolen farm tools (e.g. 'Goblins burst from the tall grass waving stolen farm tools.'). |
| minor | duplication | Enemy_DewdropSlime | Its stats (55 HP / 6 dmg / 0.7 speed / 60 XP) nearly repeat Rogue Slime's (50 / 6 / 0.7 / 50) in the same region; only the weaknesses set it apart. | Push the stats further apart, e.g. a slower, tankier slime (around 70 HP, 0.6 speed), so the two slime cards play differently. |

*Sounds like the game:* "A glossy slime that shivers across the meadow, gathering dew as it goes." It describes an ordinary valley slime plainly and sincerely, and ties it to a real place.

### Final

| Card | Type | Text | Stats | Loot |
|---|---|---|---|---|
| **Grisk Goblin** (`Enemy_GriskGoblin`) | Enemy | A wiry goblin clutching a stolen kitchen knife. | HP 45 · dmg 6 · spd 1 · XP 65 | GriskSkinningKnife, Fibers, IronOre |
| **Burrow Badger** (`Enemy_BurrowBadger`) | Enemy | A stocky badger fresh from its den, claws out and in no mood to share the field. | HP 65 · dmg 8 · spd 0.8 · XP 80 | DroverLeggings, WolfPelt, Fibers |
| **Dewdrop Slime** (`Enemy_DewdropSlime`) | Enemy | A glossy slime that shivers across the meadow, gathering dew as it goes. | HP 55 · dmg 6 · spd 0.7 · XP 60 | HedgerowCharm, HealingHerbs, Fibers |
| **Goblin Raiding Party** (`Ambush_GoblinRaidingParty`) | Ambush | Spears rattle in the tall grass as goblins rush the road. | HP 100 · dmg 10 · spd 1 · XP 135 | ThreshingFlail, BarnyardThrowingAxe, GatepostBuckler, Rope, IronOre |
| **Wild Herb Patch** (`Resource_WildHerbPatch`) | Resource | Fragrant healing herbs growing thick along a sunny hedgerow. | 20→3 HealingHerbs / 60→3 Fibers / 100→1 HealingHerbs | — |
