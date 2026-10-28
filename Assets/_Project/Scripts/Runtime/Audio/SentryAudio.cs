using GravBoots.Hazards;
using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>
    /// A sentry's sounds, positioned at it: a sinister tone when it locks on, the shot, and its
    /// destruction. Added at runtime by <see cref="GameAudio"/>.
    /// </summary>
    public class SentryAudio : MonoBehaviour
    {
        SoundBank bank;
        Sentry sentry;
        AudioSource source;
        SentryState lastState;

        public void Bind(SoundBank soundBank)
        {
            bank = soundBank;
            sentry = GetComponent<Sentry>();
            source = GameAudio.Source(gameObject, true);
            lastState = sentry.State;
            sentry.Fired += OnFired;
            sentry.Destroyed += OnDestroyed;
        }

        void OnDestroy()
        {
            if (sentry == null)
                return;
            sentry.Fired -= OnFired;
            sentry.Destroyed -= OnDestroyed;
        }

        void OnFired(bool hit) => GameAudio.Play(source, bank.sentryFire, 0.05f);

        void OnDestroyed() => GameAudio.Play(source, bank.sentryDestroyed, 0.05f);

        void Update()
        {
            if (sentry == null)
                return;
            SentryState state = sentry.State;
            // Locking on from rest; a re-lock straight after firing stays quiet.
            if (state == SentryState.Lock && lastState == SentryState.Idle)
                GameAudio.Play(source, bank.sentryAlert, 0.03f);
            lastState = state;
        }
    }
}
