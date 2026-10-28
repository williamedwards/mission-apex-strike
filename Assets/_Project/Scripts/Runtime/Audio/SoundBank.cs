using System;
using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>A clip and how loud to play it.</summary>
    [Serializable]
    public struct Sound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    /// <summary>
    /// Every sound in the game and its volume, in one place to rebalance. Loaded from
    /// Resources/SoundBank by <see cref="GameAudio"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GravBoots/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Player")]
        [Tooltip("Single steps; one is picked at random (never the same twice running).")]
        public AudioClip[] footsteps = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float footstepVolume = 0.5f;
        public Sound breathing;
        public Sound bootsOn;
        public Sound bootsOff;
        [Tooltip("Loops while the boots are magnetized.")]
        public Sound bootsHum;
        public Sound deathGroan;

        [Header("Sentries")]
        public Sound sentryAlert;
        public Sound sentryFire;
        public Sound sentryDestroyed;

        [Header("Elevators")]
        [Tooltip("Loops while an elevator's platform is moving.")]
        public Sound elevatorLoop;
        public Sound elevatorStop;

        [Header("Moving platforms")]
        [Tooltip("Loops while a platform moves, louder the faster it goes.")]
        public Sound platformLoop;
        [Tooltip("Plays as a platform comes to rest at an end.")]
        public Sound platformStop;

        public static SoundBank Load() => Resources.Load<SoundBank>("SoundBank");
    }
}
