using GravBoots.Level;
using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>
    /// A moving platform's sounds, on the platform: a glide loop that swells and fades with its
    /// speed (trips ease in and out), and a clunk as it comes to rest at an end. Added at runtime
    /// by <see cref="GameAudio"/> to movers that aren't part of an elevator.
    /// </summary>
    public class PlatformAudio : MonoBehaviour
    {
        /// <summary>Below this fraction of its average speed the platform counts as stopped.</summary>
        const float StillFraction = 0.02f;

        SoundBank bank;
        KinematicMover mover;
        AudioSource glide, effects;
        bool wasMoving;

        public void Bind(SoundBank soundBank)
        {
            bank = soundBank;
            mover = GetComponent<KinematicMover>();
            GameObject host = mover.platform != null ? mover.platform.gameObject : gameObject;
            glide = GameAudio.Source(host, true, true);
            effects = GameAudio.Source(host, true);
            GameAudio.StartLoop(glide, bank.platformLoop, 0f);
            // Platforms sharing a clip shouldn't drone in unison.
            if (glide.clip != null)
                glide.time = Random.Range(0f, glide.clip.length);
        }

        void Update()
        {
            if (mover == null)
                return;
            float fraction = mover.PathSpeed / Mathf.Max(0.01f, mover.speed);
            glide.volume = bank.platformLoop.volume * Mathf.Clamp01(fraction);
            glide.pitch = Mathf.Lerp(0.85f, 1.05f, Mathf.Clamp01(fraction * 0.7f));
            bool moving = fraction > StillFraction;
            if (wasMoving && !moving)
                GameAudio.Play(effects, bank.platformStop, 0.05f);
            wasMoving = moving;
        }
    }
}
