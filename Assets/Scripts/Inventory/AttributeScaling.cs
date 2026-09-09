using System;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // One "this weapon scales off X" entry on an ItemData — a weapon can carry several
    // (a magic sword scaling off both Constitution and Spirit, a heavy bow off both
    // Agility and Constitution), and different tiers of the same weapon concept just
    // author different coefficients. Only meaningful when the item's category is Weapon.
    [Serializable]
    public class AttributeScaling
    {
        public PlayerAttribute attribute;

        [Tooltip("Basic-attack damage bonus per point in this attribute.")]
        public float coefficient = 0.5f;
    }
}
