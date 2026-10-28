using UnityEngine;

namespace GravBoots.Level
{
    public enum ElevatorRole
    {
        /// <summary>Brings the player up into the chamber at the start of a run.</summary>
        Start,
        /// <summary>Carries the player up and out once they step on (its platform carries the <see cref="LevelExit"/>).</summary>
        Exit,
    }

    /// <summary>
    /// A tube elevator: a <see cref="KinematicMover"/> in <see cref="MoverMode.OnStep"/> mode
    /// whose platform rises when the player stands on it. A start elevator's
    /// <see cref="boardingSpawn"/> is on the platform at the bottom; while the player rides up
    /// from it the <see cref="LevelSession"/> holds the controls, then makes
    /// <see cref="arrivalSpawn"/> (on the platform at the top) the checkpoint, so deaths don't
    /// replay the ride. An exit elevator's platform carries the chamber's <see cref="LevelExit"/>.
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        public ElevatorRole role;
        public KinematicMover mover;
        [Tooltip("Start: where a run begins, on the platform at the bottom of the shaft.")]
        public SpawnPoint boardingSpawn;
        [Tooltip("Start: where the player respawns once arrived, on the platform at the top.")]
        public SpawnPoint arrivalSpawn;

        /// <summary>The platform has made its trip and stopped at the top.</summary>
        public bool HasArrived => mover != null && mover.Triggered && mover.AtEnd;
    }
}
