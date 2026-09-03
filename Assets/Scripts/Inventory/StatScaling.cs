using System;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // One "this weapon scales off X" entry on an ItemData — a weapon can carry several
    // (a magic sword scaling off both Strength and Spirit, a heavy bow off both
    // Dexterity and Strength), and different tiers of the same weapon concept just
    // author different coefficients. Only meaningful when the item's category is Weapon.
    [Serializable]
    public class StatScaling
    {
        public PlayerStat stat;

        [Tooltip("Basic-attack damage bonus per point in this stat.")]
        public float coefficient = 0.5f;
    }
}
