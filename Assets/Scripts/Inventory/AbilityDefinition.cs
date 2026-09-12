using UnityEngine;

namespace DoomScroller.Inventory
{
    // One catalog entry WeaponGenerator can roll onto a weapon's abilities array — a
    // reusable, browsable template (see the Weapon Catalog Editor window), not a
    // per-weapon one-off. Drop an asset under Assets/Resources/Abilities/ and it's
    // automatically in the pool, same "no registration step" convention EncounterData/
    // Items already use. `data` is copied by value into the generated ItemData.abilities
    // array — nothing at runtime holds a reference back to this asset, so
    // CombatSimulator/PlayerLoadout/ProfileView need no changes at all.
    [CreateAssetMenu(menuName = "DoomScroller/Ability Definition", fileName = "NewAbility")]
    public class AbilityDefinition : ScriptableObject
    {
        public WeaponAbilityData data;

        [Tooltip("Which weapon archetypes can roll this ability — a ranged Bow ability " +
                  "shouldn't land on a Sword.")]
        public WeaponType[] allowedWeaponTypes;
    }
}
