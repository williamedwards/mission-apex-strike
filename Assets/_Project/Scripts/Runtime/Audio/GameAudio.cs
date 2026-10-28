using GravBoots.Hazards;
using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravBoots.Audio
{
    /// <summary>
    /// Gives each scene its sound when it loads: the player, every sentry and every elevator get
    /// their audio component, playing clips from the <see cref="SoundBank"/>. Nothing is saved
    /// into scenes, so every chamber (and the test bed) sounds the same without editing them.
    /// </summary>
    public static class GameAudio
    {
        static bool listening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (!listening)
            {
                SceneManager.sceneLoaded += (scene, mode) => Attach();
                listening = true;
            }
            Attach();
        }

        /// <summary>Adds sound to everything in the open scenes that hasn't got it yet.</summary>
        public static void Attach()
        {
            SoundBank bank = SoundBank.Load();
            if (bank == null)
                return;
            foreach (PlayerMotor motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                if (!motor.TryGetComponent(out PlayerAudio _))
                    motor.gameObject.AddComponent<PlayerAudio>().Bind(bank);
            foreach (Sentry sentry in Object.FindObjectsByType<Sentry>(FindObjectsSortMode.None))
                if (!sentry.TryGetComponent(out SentryAudio _))
                    sentry.gameObject.AddComponent<SentryAudio>().Bind(bank);
            foreach (Elevator elevator in Object.FindObjectsByType<Elevator>(FindObjectsSortMode.None))
                if (!elevator.TryGetComponent(out ElevatorAudio _))
                    elevator.gameObject.AddComponent<ElevatorAudio>().Bind(bank);
        }

        /// <summary>An audio source on <paramref name="host"/>: flat (2D) for the player's own sounds, positioned (3D) for things in the world.</summary>
        internal static AudioSource Source(GameObject host, bool positional, bool loop = false)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = positional ? 1f : 0f;
            if (positional)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 35f;
                source.dopplerLevel = 0f;
            }
            return source;
        }

        internal static void Play(AudioSource source, Sound sound, float pitchJitter = 0f)
        {
            if (source == null || sound.clip == null)
                return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(sound.clip, sound.volume);
        }

        internal static void StartLoop(AudioSource source, Sound sound, float volume)
        {
            if (source == null || sound.clip == null)
                return;
            source.clip = sound.clip;
            source.volume = volume;
            if (!source.isPlaying)
                source.Play();
        }
    }
}
