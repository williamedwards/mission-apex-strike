using UnityEngine;

namespace GravBoots
{
    /// <summary>Physics layers and tags the game relies on (see ProjectSettings/TagManager).</summary>
    public static class Layers
    {
        public const int Player = 8;
        /// <summary>Surfaces the magnetic boots stick to.</summary>
        public const int Magnetic = 9;

        /// <summary>Tag for colliders that kill on contact; the landing marker shows them in red.</summary>
        public const string LethalTag = "Lethal";

        public static bool IsMagnetic(Collider collider) => collider != null && collider.gameObject.layer == Magnetic;
    }
}
