using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>
    /// Background music: plays the <see cref="SoundBank"/>'s tracks in order, crossfading from
    /// each into the next near its end, and keeps playing across scene loads. Created once by
    /// <see cref="GameAudio"/>. Right-click the component in the Inspector for Next Track.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float FadeSeconds = 3f;

        SoundBank bank;
        AudioSource current, next;
        int track = -1;

        public static MusicPlayer Instance { get; private set; }

        public static void Ensure(SoundBank soundBank)
        {
            if (Instance != null || soundBank.music == null || soundBank.music.Length == 0)
                return;
            var host = new GameObject("Music");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<MusicPlayer>();
            Instance.bank = soundBank;
            Instance.current = GameAudio.Source(host, false);
            Instance.next = GameAudio.Source(host, false);
            Instance.Advance(false);
        }

        [ContextMenu("Next Track")]
        public void NextTrack() => Advance(true);

        /// <summary>Starts the next track: crossfading into it, or at once if nothing is playing.</summary>
        void Advance(bool crossfade)
        {
            track = (track + 1) % bank.music.Length;
            AudioClip clip = bank.music[track];
            if (clip == null)
                return;
            (current, next) = (next, current);
            current.clip = clip;
            current.volume = crossfade ? 0f : bank.musicVolume;
            current.Play();
            if (!crossfade)
                next.Stop();
        }

        void Update()
        {
            float step = bank.musicVolume * Time.unscaledDeltaTime / FadeSeconds;
            current.volume = Mathf.MoveTowards(current.volume, bank.musicVolume, step);
            next.volume = Mathf.MoveTowards(next.volume, 0f, step);
            if (next.isPlaying && next.volume <= 0f)
                next.Stop();
            // Near the end (or past it, if a frame was long), crossfade into the next track.
            if (current.clip != null && (!current.isPlaying || current.clip.length - current.time < FadeSeconds))
                Advance(true);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
