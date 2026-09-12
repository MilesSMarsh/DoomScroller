using UnityEngine;

namespace DoomScroller.Inventory
{
    // One catalog entry WeaponGenerator can roll onto a weapon's passiveEffects array —
    // same shape/purpose as AbilityDefinition, just for the always-on (no cast/cooldown)
    // side of a weapon's kit. Whether this specific passive scales with an attribute is
    // authored on `effect` itself (StatusEffectData.scalesWithAttribute), not decided at
    // roll time — some passives are flat, some grow with a stat, both are valid catalog
    // entries.
    [CreateAssetMenu(menuName = "DoomScroller/Passive Definition", fileName = "NewPassive")]
    public class PassiveDefinition : ScriptableObject
    {
        public StatusEffectData effect;

        [Tooltip("Which weapon archetypes can roll this passive.")]
        public WeaponType[] allowedWeaponTypes;
    }
}
