using UnityEngine;

namespace DoomScroller.Inventory
{
    // Which authoring asset a RecipeDefinition's output actually comes from — a fixed
    // hand-authored ItemData (the original/simple case), or a rolled WeaponDefinition/
    // EquipmentDefinition at a specific (fixed-per-recipe, not randomly rolled) rarity.
    // A recipe always yields a KNOWN result, same as every reference crafting game
    // (Terraria/Minecraft/Valheim) — different rarity tiers of the same weapon become
    // different recipes (gated behind different prerequisites), not a loot roll.
    public enum RecipeOutputKind
    {
        FixedItem,
        Weapon,
        Equipment,
        Material
    }

    // A hand-authored crafting recipe — listed/edited by the Equipment Menu window
    // (Assets/Editor/EquipmentMenuWindow.cs), read by VillageBlacksmithView via
    // RecipeCatalog.RecipesFor. Gating (PlayerLoadout.IsRecipeUnlocked) and crafting
    // (PlayerLoadout.TryCraftRecipe) both live on PlayerLoadout, same as every other
    // inventory mutation.
    [CreateAssetMenu(menuName = "DoomScroller/Recipe Definition", fileName = "NewRecipe")]
    public class RecipeDefinition : ScriptableObject
    {
        [Tooltip("Stable identity for save data — deliberately not the asset name/title, " +
                  "so renaming this recipe later can't silently break a save file's " +
                  "craftedRecipeIds. Auto-assigned once at creation by the catalog window.")]
        public string recipeId;

        public RecipeOutputKind outputKind;
        public ItemData outputItem; // FixedItem
        public WeaponDefinition outputWeapon; // Weapon
        public EquipmentDefinition outputEquipment; // Equipment
        public MaterialDefinition outputMaterial; // Material — e.g. Rope crafted from Fibers
        public ItemRarity outputRarity; // Weapon/Equipment/Material only

        [Tooltip("Self-authored, like every other placeholder number this session — a " +
                  "rolled Weapon/Equipment output has no single natural goldValue to " +
                  "derive a price from, so every recipe just authors its own cost " +
                  "directly (the Expertise-based discount still applies on top, same as " +
                  "everywhere else).")]
        public int goldCost = 20;

        public RecipeIngredient[] ingredients;

        [Tooltip("Recipes that must already be crafted at least once before this one is " +
                  "available — the actual \"tree\" edges. Empty/null for a root recipe.")]
        public RecipeDefinition[] prerequisiteRecipes;

        public string OutputTitle => outputKind switch
        {
            RecipeOutputKind.Weapon => outputWeapon != null ? outputWeapon.weaponName : "(no weapon)",
            RecipeOutputKind.Equipment => outputEquipment != null ? outputEquipment.equipmentName : "(no equipment)",
            RecipeOutputKind.Material => outputMaterial != null ? outputMaterial.materialName : "(no material)",
            _ => outputItem != null ? outputItem.title : "(no output item)"
        };

        public ItemCategory? OutputCategory => outputKind switch
        {
            RecipeOutputKind.Weapon => ItemCategory.Weapon,
            RecipeOutputKind.Equipment => outputEquipment != null ? outputEquipment.category : null,
            RecipeOutputKind.Material => ItemCategory.Material,
            _ => outputItem != null ? outputItem.category : null
        };

        public Sprite OutputIcon => outputKind switch
        {
            RecipeOutputKind.Weapon => outputWeapon != null ? outputWeapon.icon : null,
            RecipeOutputKind.Equipment => outputEquipment != null ? outputEquipment.icon : null,
            RecipeOutputKind.Material => outputMaterial != null ? outputMaterial.icon : null,
            _ => outputItem != null ? outputItem.icon : null
        };

        public Color OutputColor => outputKind switch
        {
            RecipeOutputKind.Weapon => outputWeapon != null ? outputWeapon.itemColor : Color.gray,
            RecipeOutputKind.Equipment => outputEquipment != null ? outputEquipment.itemColor : Color.gray,
            RecipeOutputKind.Material => outputMaterial != null ? outputMaterial.itemColor : Color.gray,
            _ => outputItem != null ? outputItem.itemColor : Color.gray
        };

        // Weapon/Equipment/Material kinds already commit to a fixed rarity (outputRarity
        // — see RecipeOutputKind's own doc comment); a FixedItem's rarity is whatever
        // its hand-authored ItemData carries.
        public ItemRarity OutputRarity => outputKind switch
        {
            RecipeOutputKind.Weapon or RecipeOutputKind.Equipment or RecipeOutputKind.Material => outputRarity,
            _ => outputItem != null ? outputItem.rarity : ItemRarity.Common
        };

        // "+22 Dmg · 1.1/s" / "+18 Defence" — the Blacksmith Craft tab's damage/speed
        // line. Unlike ItemData.WeaponDetailSummary this DOES include damage (a recipe
        // row shows no damage figure otherwise) but never abilities — nothing's been
        // rolled yet for a Weapon-kind recipe (see DescribePossibleAbilities in
        // VillageBlacksmithView for the tap-to-reveal possible pool instead).
        public string OutputStatSummary
        {
            get
            {
                var rarityIndex = (int)outputRarity;
                return outputKind switch
                {
                    RecipeOutputKind.Weapon => outputWeapon != null && outputWeapon.statsByRarity != null
                                                && rarityIndex < outputWeapon.statsByRarity.Length
                        ? $"+{outputWeapon.statsByRarity[rarityIndex].baseDamage} Dmg · {outputWeapon.attackSpeed:0.0}/s"
                        : "",
                    RecipeOutputKind.Equipment => outputEquipment != null && outputEquipment.powerByRarity != null
                                                   && rarityIndex < outputEquipment.powerByRarity.Length
                        ? $"+{outputEquipment.powerByRarity[rarityIndex]} {ItemCategoryInfo.Label(outputEquipment.category)}"
                        : "",
                    _ => outputItem != null ? outputItem.statSummary : ""
                };
            }
        }
    }
}
