using UnityEngine;

namespace DoomScroller.Inventory
{
    // A crafting-material concept, authored once — actual owned copies are ItemData
    // instances generated at a specific rarity from this (MaterialGenerator.
    // GenerateFromDefinition), same pattern as WeaponDefinition/EquipmentDefinition.
    // Deliberately carries no per-rarity fields: rarity is a property of a generated
    // instance, not something authored per tier here.
    [CreateAssetMenu(menuName = "DoomScroller/Material Definition", fileName = "NewMaterial")]
    public class MaterialDefinition : ScriptableObject
    {
        public string materialName;
        public Sprite icon;
        public Color itemColor = Color.gray;

        [Tooltip("Base value at Common rarity — scaled by RewardGenerator.RarityGoldValue's " +
                  "own ratios for higher tiers, same table every other rarity-scaled value uses.")]
        public int goldValue = 5;
    }
}
