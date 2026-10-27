using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Something a drop can destroy by landing on it (a sentry); the landing marker highlights it.</summary>
    public interface IStompTarget
    {
        /// <summary>Whether a player falling straight down (world gravity) through <paramref name="point"/> would destroy it.</summary>
        bool WouldStomp(Vector3 point);
    }
}
