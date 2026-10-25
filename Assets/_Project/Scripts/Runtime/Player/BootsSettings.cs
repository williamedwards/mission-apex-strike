using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Tuning values for <see cref="MagneticBoots"/>; editable while playing.</summary>
    [CreateAssetMenu(menuName = "GravBoots/Boots Settings", fileName = "BootsSettings")]
    public class BootsSettings : ScriptableObject
    {
        [Header("Following surfaces")]
        [Tooltip("Up follows the normal of the magnetic ground at this rate (degrees per second). It must outpace walking speed / curve radius: 6 m/s round a 1.5 m curve is about 230°/s.")]
        public float followDegreesPerSecond = 540f;
        [Tooltip("The sharpest crease the boots follow. Curves are made of segments well under this; right-angle joins are far over it, so they can't be crossed.")]
        public float followMaxAngle = 30f;

        [Header("Losing contact")]
        [Tooltip("How long the boots tolerate no magnetic contact (seams, small bumps) before letting go.")]
        public float contactGrace = 0.12f;
        [Tooltip("Allow a small jump while magnetized; the boots hold on until you land again.")]
        public bool allowHop = true;
        [Tooltip("A hop longer than this lets go, as if you'd walked off.")]
        public float maxHopAirTime = 1.2f;

        [Header("Dropping")]
        [Tooltip("Speed away from the surface when the boots switch off, so you don't scrape down a wall.")]
        public float pushOffSpeed = 1.5f;
    }
}
