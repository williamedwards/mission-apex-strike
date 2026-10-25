using GravBoots.Player;
using UnityEngine;

namespace GravBoots.UI
{
    /// <summary>
    /// Minimal HUD: boots state, and a crosshair that turns blue over magnetic surfaces in
    /// reach, so players can read which surfaces connect (pillar 3).
    /// </summary>
    public class BootsHud : MonoBehaviour
    {
        [SerializeField] MagneticBoots boots;
        [SerializeField] LandingMarker landingMarker;
        [SerializeField] float magneticReadRange = 40f;
        [SerializeField] Color magneticColor = new Color(0.35f, 0.8f, 1f);
        [SerializeField] Color neutralColor = new Color(1f, 1f, 1f, 0.6f);

        GUIStyle style;
        float refusedFlash;

        public bool AimingAtMagnetic { get; private set; }

        void OnEnable()
        {
            if (boots != null)
                boots.ActivationRefused += OnRefused;
        }

        void OnDisable()
        {
            if (boots != null)
                boots.ActivationRefused -= OnRefused;
        }

        void OnRefused() => refusedFlash = 1f;

        void Update()
        {
            refusedFlash = Mathf.Max(0f, refusedFlash - Time.deltaTime * 1.5f);
            Camera view = Camera.main;
            AimingAtMagnetic = view != null && Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)),
                out RaycastHit hit, magneticReadRange, ~(1 << Layers.Player), QueryTriggerInteraction.Ignore)
                && Layers.IsMagnetic(hit.collider);
        }

        void OnGUI()
        {
            if (boots == null)
                return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            Color previous = GUI.color;

            GUI.color = AimingAtMagnetic ? magneticColor : neutralColor;
            float size = AimingAtMagnetic ? 10f : 6f;
            GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), Texture2D.whiteTexture);

            string label;
            switch (boots.State)
            {
                case BootsState.Magnetized:
                    GUI.color = magneticColor;
                    label = "BOOTS  MAGNETIZED";
                    break;
                case BootsState.Dropping:
                    GUI.color = landingMarker != null && landingMarker.IsShowing && landingMarker.IsLethal
                        ? new Color(1f, 0.3f, 0.25f) : Color.white;
                    label = "BOOTS  OFF — DROPPING";
                    break;
                default:
                    GUI.color = Color.Lerp(new Color(1f, 1f, 1f, 0.7f), new Color(1f, 0.4f, 0.3f), refusedFlash);
                    label = refusedFlash > 0f ? "NO MAGNETIC SURFACE UNDERFOOT" : "BOOTS  OFF";
                    break;
            }
            GUI.Label(new Rect(cx - 250f, Screen.height - 60f, 500f, 30f), label, style);
            GUI.color = previous;
        }
    }
}
