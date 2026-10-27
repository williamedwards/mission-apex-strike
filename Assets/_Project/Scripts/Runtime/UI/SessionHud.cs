using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;

namespace GravBoots.UI
{
    /// <summary>
    /// The run's HUD: health, time and deaths, a "checkpoint" notice, the fade on death and
    /// respawn, and the completion panel. Placeholder IMGUI until the visual pass.
    /// </summary>
    public class SessionHud : MonoBehaviour
    {
        [SerializeField] LevelSession session;
        [SerializeField] Health health;
        [SerializeField] float noticeSeconds = 1.5f;
        [SerializeField] float fadeInSeconds = 0.25f;
        [SerializeField] float damageFlashSeconds = 0.3f;
        [SerializeField] Color accent = new Color(0.3f, 1f, 0.6f);

        GUIStyle small, big;
        float noticeTime, damageFlash;
        float sinceRespawn = float.MaxValue;
        bool started;

        void OnEnable()
        {
            if (session == null)
                return;
            session.CheckpointChanged += OnCheckpoint;
            session.Respawned += OnRespawned;
            if (health != null)
                health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (session == null)
                return;
            session.CheckpointChanged -= OnCheckpoint;
            session.Respawned -= OnRespawned;
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        void OnDamaged(float amount) => damageFlash = damageFlashSeconds;

        // The first checkpoint is the start, which needs no notice.
        void OnCheckpoint(SpawnPoint spawn) => noticeTime = spawn is Checkpoint ? noticeSeconds : 0f;

        void OnRespawned()
        {
            if (started)
                sinceRespawn = 0f;
            started = true;
        }

        void Update()
        {
            noticeTime = Mathf.Max(0f, noticeTime - Time.deltaTime);
            damageFlash = Mathf.Max(0f, damageFlash - Time.deltaTime);
            if (sinceRespawn < float.MaxValue)
                sinceRespawn += Time.deltaTime;
        }

        void OnGUI()
        {
            if (session == null)
                return;
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Color previous = GUI.color;
            float w = Screen.width, h = Screen.height;

            if (health != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                float x = w * 0.5f - 102f;
                GUI.DrawTexture(new Rect(x, 20f, 204f, 14f), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.25f), accent, health.Current / health.Max);
                GUI.DrawTexture(new Rect(x + 2f, 22f, 200f * health.Current / health.Max, 10f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            small.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(w - 320f, 16f, 300f, 24f), $"{FormatTime(session.ElapsedTime)}    DEATHS {session.Deaths}", small);

            if (noticeTime > 0f)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(noticeTime / 0.4f));
                GUI.Label(new Rect(0f, h * 0.3f, w, 40f), "CHECKPOINT", big);
            }

            if (damageFlash > 0f)
            {
                GUI.color = new Color(1f, 0.1f, 0.05f, 0.35f * damageFlash / damageFlashSeconds);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }

            float fade = session.State == SessionState.Dying
                ? session.DyingProgress
                : 1f - Mathf.Clamp01(sinceRespawn / fadeInSeconds);
            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }

            if (session.State == SessionState.Complete)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(0f, h * 0.35f, w, h * 0.3f), Texture2D.whiteTexture);
                GUI.color = accent;
                GUI.Label(new Rect(0f, h * 0.38f, w, 50f), "CHAMBER COMPLETE", big);
                GUI.color = Color.white;
                small.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(0f, h * 0.47f, w, 30f), $"Time {FormatTime(session.ElapsedTime)}    Deaths {session.Deaths}", small);
                GUI.Label(new Rect(0f, h * 0.54f, w, 30f), "Jump to continue", small);
            }
            GUI.color = previous;
        }

        static string FormatTime(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.00}";
    }
}
