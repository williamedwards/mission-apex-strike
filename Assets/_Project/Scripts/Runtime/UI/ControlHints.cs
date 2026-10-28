using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravBoots.UI
{
    /// <summary>
    /// Control hints near the bottom of the screen, a key cap and what it does, chosen by
    /// <see cref="HintRules"/> from what the player is doing. Added to the player at runtime when
    /// a scene loads, so no scene needs editing; what the player has learned lasts the session.
    /// </summary>
    public class ControlHints : MonoBehaviour
    {
        /// <summary>A magnetic curve this close (from the player's middle) counts as next to it.</summary>
        const float RampReach = 2.5f;
        /// <summary>
        /// A ledge counts when there's something at knee height this far ahead and clear space
        /// at the jump's height (MotorSettings.jumpHeight, 1.3 m) above it.
        /// </summary>
        const float LedgeReach = 0.9f, KneeHeight = 0.35f, ClearHeight = 1.3f;
        const float FadeSeconds = 0.25f;

        static HintRules rules;
        static bool listening;

        PlayerMotor motor;
        MagneticBoots boots;
        PlayerInputReader input;
        LevelSession session;
        bool wasGrounded;
        ControlHint wanted, shown;
        float alpha;
        readonly Collider[] nearby = new Collider[16];
        GUIStyle capStyle, textStyle;
        int styledFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => rules = null;

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

        static void Attach()
        {
            rules ??= new HintRules();
            foreach (PlayerMotor m in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                if (!m.TryGetComponent(out ControlHints _))
                    m.gameObject.AddComponent<ControlHints>();
        }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            boots = GetComponent<MagneticBoots>();
            if (!TryGetComponent(out input))
                input = FindAnyObjectByType<PlayerInputReader>();
            session = FindAnyObjectByType<LevelSession>();
            wasGrounded = motor.IsGrounded;
        }

        void Update()
        {
            Vector3 up = motor.BodyUp;
            bool magnetized = boots != null && boots.IsMagnetized;
            bool bootsOff = boots == null || boots.State == BootsState.Off;
            var situation = new HintSituation
            {
                InControl = session == null || session.State == SessionState.Playing,
                Grounded = motor.IsGrounded,
                Steering = input != null && input.Move.sqrMagnitude > 0.04f,
                Speed = Vector3.ProjectOnPlane(motor.Velocity, up).magnitude,
                BootsOff = bootsOff,
                Magnetized = magnetized,
                OnMagneticGround = motor.IsGrounded && Layers.IsMagnetic(motor.GroundCollider),
                NearMagneticRamp = bootsOff && NearMagneticRamp(up),
                LedgeAhead = bootsOff && motor.IsGrounded && LedgeAhead(up),
                Jumped = wasGrounded && !motor.IsGrounded && Vector3.Dot(motor.Velocity, up) > 1f,
            };
            wasGrounded = motor.IsGrounded;
            wanted = rules.Update(situation, Time.deltaTime);

            // Fade the current hint out before bringing in a different one.
            float step = Time.deltaTime / FadeSeconds;
            if (wanted == shown)
                alpha = Mathf.MoveTowards(alpha, shown == ControlHint.None ? 0f : 1f, step);
            else
            {
                alpha = Mathf.MoveTowards(alpha, 0f, step);
                if (alpha <= 0f)
                    shown = wanted;
            }
        }

        bool NearMagneticRamp(Vector3 up)
        {
            int count = Physics.OverlapSphereNonAlloc(motor.Position + up * 0.9f, RampReach, nearby,
                1 << Layers.Magnetic, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (nearby[i].GetComponentInParent<CurvedRamp>() != null)
                    return true;
            return false;
        }

        bool LedgeAhead(Vector3 up)
        {
            // Ahead: where the player is steering, else where they face.
            Vector3 forward = Vector3.ProjectOnPlane(motor.Velocity, up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            if (forward.sqrMagnitude < 1e-4f)
                return false;
            forward.Normalize();
            int mask = ~(1 << Layers.Player);
            Vector3 feet = motor.Position;
            return Physics.Raycast(feet + up * KneeHeight, forward, out RaycastHit hit, LedgeReach, mask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(hit.normal, up) < 0.5f
                && !hit.collider.CompareTag(Layers.LethalTag)
                && !Physics.Raycast(feet + up * ClearHeight, forward, LedgeReach + 0.3f, mask, QueryTriggerInteraction.Ignore);
        }

        void OnGUI()
        {
            if (shown == ControlHint.None || alpha <= 0f)
                return;
            string key, action;
            switch (shown)
            {
                case ControlHint.Move: key = "W A S D"; action = "Move"; break;
                case ControlHint.Jump: key = "SPACE"; action = "Jump"; break;
                case ControlHint.Magnetize: key = "E"; action = "Magnetize boots"; break;
                default: key = "E"; action = "Demagnetize boots"; break;
            }

            // Sized to the screen: 26 px text at 720p.
            float unit = Screen.height / 720f;
            if (styledFor != Screen.height || capStyle == null)
            {
                styledFor = Screen.height;
                capStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(22f * unit) };
                textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(26f * unit) };
            }

            Vector2 keySize = capStyle.CalcSize(new GUIContent(key));
            Vector2 textSize = textStyle.CalcSize(new GUIContent(action));
            float pad = 14f * unit, gap = 14f * unit, capH = 44f * unit;
            float capW = Mathf.Max(capH, keySize.x + 24f * unit);
            float w = pad + capW + gap + textSize.x + pad, h = capH + 2f * pad * 0.6f;
            var panel = new Rect((Screen.width - w) * 0.5f, Screen.height - 150f * unit - h, w, h);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            var cap = new Rect(panel.x + pad, panel.y + (h - capH) * 0.5f, capW, capH);
            Color capColor = shown == ControlHint.Magnetize || shown == ControlHint.Demagnetize
                ? new Color(0.35f, 0.8f, 1f) : Color.white;
            GUI.color = new Color(capColor.r, capColor.g, capColor.b, alpha);
            float b = Mathf.Max(1f, 2f * unit);
            GUI.DrawTexture(new Rect(cap.x, cap.y, cap.width, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.x, cap.yMax - b, cap.width, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.x, cap.y, b, cap.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.xMax - b, cap.y, b, cap.height), Texture2D.whiteTexture);
            GUI.Label(cap, key, capStyle);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(cap.xMax + gap, panel.y, textSize.x + pad, h), action, textStyle);
            GUI.color = previous;
        }
    }
}
