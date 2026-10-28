using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// The magnetized look: energy rings floating round the ankles, a glow under each sole and
    /// rising sparks. Hidden while the boots are off; fades in on clamping (with a burst of
    /// sparks) and out on letting go.
    /// </summary>
    public class BootsGlow : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] MagneticBoots boots;
        [Tooltip("Spun about their own up and bobbed along it; odd entries spin the other way.")]
        [SerializeField] Transform[] rings;
        [SerializeField] Renderer[] glows;
        [SerializeField] ParticleSystem[] sparks;
        [SerializeField] float fadeInTime = 0.12f;
        [SerializeField] float fadeOutTime = 0.3f;
        [SerializeField] float spinDegreesPerSecond = 160f;
        [SerializeField] float bobHeight = 0.012f;
        [SerializeField] float bobRate = 3f;
        [SerializeField] int clampBurst = 14;

        Color[] glowColors;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        MaterialPropertyBlock block;
        bool on;
        float intensity;

        public float Intensity => intensity;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            // Each glow keeps its material's colour; only its alpha follows the fade.
            glowColors = new Color[glows.Length];
            for (int i = 0; i < glows.Length; i++)
                glowColors[i] = glows[i] != null ? glows[i].sharedMaterial.GetColor(BaseColorId) : Color.white;
            restPositions = new Vector3[rings.Length];
            restRotations = new Quaternion[rings.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                restPositions[i] = rings[i].localPosition;
                restRotations[i] = rings[i].localRotation;
            }
        }

        void OnEnable()
        {
            if (boots != null)
                boots.StateChanged += OnStateChanged;
            on = boots != null && boots.IsMagnetized;
            intensity = on ? 1f : 0f;
            SetSparks(on, burst: false);
            Apply();
        }

        void OnDisable()
        {
            if (boots != null)
                boots.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(BootsState previous, BootsState next)
        {
            bool magnetized = next == BootsState.Magnetized;
            if (magnetized == on)
                return;
            on = magnetized;
            SetSparks(on, burst: on);
        }

        void Update()
        {
            intensity = Fade(intensity, on, Time.deltaTime, fadeInTime, fadeOutTime);
            Apply();
        }

        /// <summary>Moves <paramref name="current"/> toward 1 (on) or 0 (off) over the given times.</summary>
        public static float Fade(float current, bool on, float deltaTime, float fadeIn, float fadeOut)
        {
            float time = on ? fadeIn : fadeOut;
            float step = time > 0f ? deltaTime / time : 1f;
            return Mathf.MoveTowards(current, on ? 1f : 0f, step);
        }

        void Apply()
        {
            bool visible = intensity > 0.001f;
            float t = Time.time;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null)
                    continue;
                float direction = i % 2 == 0 ? 1f : -1f;
                Vector3 axis = restRotations[i] * Vector3.up;
                rings[i].localPosition = restPositions[i] + axis * (Mathf.Sin(t * bobRate + i) * bobHeight);
                rings[i].localRotation = restRotations[i] * Quaternion.AngleAxis(direction * spinDegreesPerSecond * t, Vector3.up);
            }
            // A slight flicker keeps the light alive; it never exceeds the fade.
            float flicker = 0.85f + 0.15f * Mathf.Sin(t * 23f) * Mathf.Sin(t * 7f);
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null)
                    continue;
                glows[i].enabled = visible;
                Color tint = glowColors[i];
                tint.a *= intensity * flicker;
                block.SetColor(BaseColorId, tint);
                glows[i].SetPropertyBlock(block);
            }
        }

        void SetSparks(bool emit, bool burst)
        {
            foreach (ParticleSystem system in sparks)
            {
                if (system == null)
                    continue;
                if (emit)
                {
                    system.Play();
                    if (burst)
                        system.Emit(clampBurst);
                }
                else
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
