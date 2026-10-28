using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>What the jump button does while the boots are magnetized.</summary>
    public enum MagnetizedJump
    {
        /// <summary>Nothing: the boots are too heavy to jump in.</summary>
        Blocked,
        /// <summary>Lets go, exactly like switching the boots off.</summary>
        LetsGo,
    }

    /// <summary>Tuning values for <see cref="MagneticBoots"/>; editable while playing.</summary>
    [CreateAssetMenu(menuName = "GravBoots/Boots Settings", fileName = "BootsSettings")]
    public class BootsSettings : ScriptableObject
    {
        [Header("Following surfaces")]
        [Tooltip("Up follows the normal of the magnetic ground at this rate (degrees per second). It must outpace walking speed / curve radius: 6 m/s round a 1.5 m curve is about 230°/s.")]
        public float followDegreesPerSecond = 540f;
        [Tooltip("The sharpest crease the boots follow. Curves are made of segments well under this; right-angle joins are far over it, so they can't be crossed.")]
        public float followMaxAngle = 30f;

        [Header("Walking magnetized")]
        [Tooltip("Top speed while magnetized (m/s): heavy and deliberate next to the free 6 m/s.")]
        public float magnetizedMoveSpeed = 3f;
        [Tooltip("How quickly you reach that speed or stop while magnetized (m/s²); lower feels heavier.")]
        public float magnetizedAcceleration = 20f;

        [Header("Losing contact")]
        [Tooltip("How long the boots tolerate no magnetic contact (seams, small bumps) before letting go.")]
        public float contactGrace = 0.12f;
        [Tooltip("What jump does while magnetized. Blocked: nothing. Lets Go: drops you, like switching the boots off.")]
        public MagnetizedJump jumpWhileMagnetized = MagnetizedJump.Blocked;

        [Header("Dropping")]
        [Tooltip("Speed away from the surface when the boots switch off, so you don't scrape down a wall.")]
        public float pushOffSpeed = 1.5f;
    }
}
