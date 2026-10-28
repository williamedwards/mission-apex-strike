using System.Collections.Generic;
using GravBoots.Debugging;
using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GravBoots.UI
{
    /// <summary>
    /// The title screen before the first run and the end screen after the exit: the game's name
    /// over slow camera moves through the chamber's rooms (<see cref="PanoramaShot"/>), with
    /// the HUD hidden. The title holds the <see cref="LevelSession"/> until the player presses
    /// Jump or clicks; the end screen follows the exit ride and shows the run's time and deaths (Jump
    /// plays again, as before). Added at runtime to any scene with a session and at least two
    /// rooms, so no scene needs editing; the title shows once per session of the game.
    /// </summary>
    public class TitleScreens : MonoBehaviour
    {
        public const string Mission = "MISSION:", Name = "APEX STRIKE";
        const float ShotSeconds = 7f, FadeSeconds = 0.6f, PromptDelay = 1f;
        /// <summary>The end screen waits this long after the exit, longer if an elevator is still moving.</summary>
        const float EndDelay = 2f, EndDelayMax = 8f;

        enum Mode { None, Title, Leaving, Waiting, End }

        struct Room
        {
            public Transform Frame;
            public float West, East, Length, Height;
        }

        static bool titleShown, listening;
        static TitleScreens current;

        readonly List<Room> rooms = new List<Room>();
        readonly List<Behaviour> hidden = new List<Behaviour>();
        LevelSession session;
        PlayerInputReader input;
        Camera gameCamera, shotCamera;
        Mode mode;
        int shot;
        float shotTime, modeTime, fade;
        float endTime;
        int endDeaths;
        Texture2D shade;
        GUIStyle small, big, huge, prompt;
        int styledFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => titleShown = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (!listening)
            {
                SceneManager.sceneLoaded += (scene, loadMode) => Attach();
                listening = true;
            }
            Attach();
        }

        // Runs after the scene's Awake and before its Start, so the session can still be held.
        static void Attach()
        {
            var levelSession = FindAnyObjectByType<LevelSession>();
            if (levelSession == null || current != null)
                return;
            var screens = new GameObject("Title Screens").AddComponent<TitleScreens>();
            if (!screens.Setup(levelSession))
            {
                Destroy(screens.gameObject);
                return;
            }
            if (!titleShown)
            {
                titleShown = true;
                screens.ShowTitle();
            }
        }

        bool Setup(LevelSession levelSession)
        {
            session = levelSession;
            FindRooms();
            if (rooms.Count < 2)
                return false;
            current = this;
            input = FindAnyObjectByType<PlayerInputReader>();
            session.Completed += OnCompleted;
            shot = Random.Range(0, rooms.Count);
            return true;
        }

        void OnDestroy()
        {
            if (session != null)
                session.Completed -= OnCompleted;
            if (current == this)
                current = null;
            if (shade != null)
                Destroy(shade);
        }

        /// <summary>
        /// The chamber's rooms: children of the Level root with side walls and a single ceiling,
        /// measured in their own frame (the Level Kit's convention: entry at the origin, +z in).
        /// </summary>
        void FindRooms()
        {
            GameObject level = GameObject.Find("Level");
            if (level == null)
                return;
            foreach (Transform frame in level.transform)
            {
                if (!Inside(frame, "Wall East", out Bounds east) || !Inside(frame, "Wall West", out Bounds west)
                    || !Inside(frame, "Ceiling", out Bounds ceiling))
                    continue;
                float length = east.max.z - 0.5f;
                if (east.min.x - west.max.x < 4f || length < 6f || ceiling.min.y < 3f)
                    continue;
                rooms.Add(new Room { Frame = frame, West = west.max.x, East = east.min.x, Length = length, Height = ceiling.min.y });
            }
        }

        /// <summary>A box child's extent in the room's frame.</summary>
        static bool Inside(Transform frame, string name, out Bounds bounds)
        {
            bounds = default;
            Transform child = frame.Find(name);
            if (child == null || !child.TryGetComponent(out BoxCollider box))
                return false;
            Vector3 centre = frame.InverseTransformPoint(child.TransformPoint(box.center));
            Vector3 size = frame.InverseTransformVector(child.TransformVector(box.size));
            bounds = new Bounds(centre, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            return true;
        }

        void ShowTitle()
        {
            session.HoldStart = true;
            // Keep the player still until the run starts: standing on the start elevator would
            // otherwise set it off while the title is up.
            if (session.Player != null)
            {
                session.Player.AutoSimulate = false;
                if (session.Player.TryGetComponent(out PlayerController controller))
                    controller.enabled = false;
            }
            Cover(Mode.Title);
            fade = 1f;
        }

        void OnCompleted()
        {
            endTime = session.ElapsedTime;
            endDeaths = session.Deaths;
            mode = Mode.Waiting;
            modeTime = 0f;
        }

        /// <summary>Takes over the view: our camera on, the game's off, the HUD hidden.</summary>
        void Cover(Mode next)
        {
            mode = next;
            modeTime = 0f;
            shotTime = 0f;
            gameCamera = Camera.main;
            if (shotCamera == null)
            {
                shotCamera = new GameObject("Panorama Camera").AddComponent<Camera>();
                shotCamera.transform.SetParent(transform, false);
                if (gameCamera != null)
                    shotCamera.CopyFrom(gameCamera);
                shotCamera.fieldOfView = 70f;
            }
            shotCamera.enabled = true;
            if (gameCamera != null)
                gameCamera.enabled = false;

            hidden.Clear();
            foreach (Behaviour b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            {
                if (b.enabled && (b is BootsHud || b is SessionHud || b is ControlHints || b is DebugOverlay))
                {
                    b.enabled = false;
                    hidden.Add(b);
                }
            }
            PlaceCamera();
        }

        void Uncover()
        {
            foreach (Behaviour b in hidden)
                if (b != null)
                    b.enabled = true;
            hidden.Clear();
            if (gameCamera != null)
                gameCamera.enabled = true;
            if (shotCamera != null)
                shotCamera.enabled = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            modeTime += dt;
            switch (mode)
            {
                case Mode.Title:
                case Mode.End:
                    AdvanceShot(dt);
                    // A click starts too: in a browser page the first click is what gives the game the keyboard.
                    if (mode == Mode.Title && modeTime > PromptDelay
                        && ((input != null && input.JumpPressed) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)))
                    {
                        mode = Mode.Leaving;
                        modeTime = 0f;
                    }
                    break;
                case Mode.Leaving:
                    AdvanceShot(dt);
                    fade = Mathf.MoveTowards(fade, 1f, dt / FadeSeconds);
                    if (fade >= 1f)
                        Begin();
                    break;
                case Mode.Waiting:
                    if (modeTime > EndDelayMax || (modeTime > EndDelay && !ElevatorMoving()))
                    {
                        fade = Mathf.MoveTowards(fade, 1f, dt / FadeSeconds);
                        if (fade >= 1f)
                            Cover(Mode.End);
                    }
                    break;
                default:
                    fade = Mathf.MoveTowards(fade, 0f, dt / FadeSeconds);
                    break;
            }
        }

        void Begin()
        {
            Uncover();
            mode = Mode.None;
            session.HoldStart = false;
            session.Player.AutoSimulate = true;
            session.Begin(session.Player);
        }

        static bool ElevatorMoving()
        {
            foreach (Elevator elevator in FindObjectsByType<Elevator>(FindObjectsSortMode.None))
                if (elevator.mover != null && elevator.mover.PathSpeed > 0.01f)
                    return true;
            return false;
        }

        /// <summary>Plays the shots in turn, fading through black between rooms.</summary>
        void AdvanceShot(float dt)
        {
            shotTime += dt;
            if (shotTime >= ShotSeconds)
            {
                shotTime -= ShotSeconds;
                shot = (shot + 1) % rooms.Count;
            }
            if (mode != Mode.Leaving)
            {
                float edge = Mathf.Min(shotTime, ShotSeconds - shotTime);
                fade = 1f - Mathf.Clamp01(edge / FadeSeconds);
            }
            PlaceCamera();
        }

        void PlaceCamera()
        {
            if (shotCamera == null || rooms.Count == 0)
                return;
            Room room = rooms[shot % rooms.Count];
            PanoramaShot move = PanoramaShot.ForRoom(room.West, room.East, room.Length, room.Height, shot % 2 == 0 ? 1 : -1);
            move.Sample(shotTime / ShotSeconds, out Vector3 position, out Quaternion rotation);
            shotCamera.transform.SetPositionAndRotation(room.Frame.TransformPoint(position), room.Frame.rotation * rotation);
        }

        void OnGUI()
        {
            GUI.depth = -100;
            Color previous = GUI.color;
            float w = Screen.width, h = Screen.height, unit = h / 720f;

            if (mode == Mode.Title || mode == Mode.Leaving || mode == Mode.End)
            {
                Styles(unit);
                // Darken the bottom of the picture behind the text.
                shade ??= Gradient();
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0f, h * 0.38f, w, h * 0.62f), shade);

                float textAlpha = mode == Mode.Leaving ? Mathf.Clamp01(1f - modeTime / FadeSeconds) : Mathf.Clamp01(modeTime / 1.2f);
                bool end = mode == Mode.End;
                float titleY = end ? h * 0.12f : h * 0.56f;
                var accent = new Color(0.35f, 0.8f, 1f);
                Label(new Rect(0f, titleY, w, 30f * unit), Spaced(Mission), small, accent, textAlpha, unit);
                Label(new Rect(0f, titleY + 26f * unit, w, 80f * unit), Spaced(Name), end ? big : huge, Color.white, textAlpha, unit);
                float lineY = titleY + (end ? 98f : 108f) * unit;
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.8f * textAlpha);
                GUI.DrawTexture(new Rect(w * 0.5f - 160f * unit, lineY, 320f * unit, Mathf.Max(1f, 2f * unit)), Texture2D.whiteTexture);

                if (end)
                {
                    Label(new Rect(0f, h * 0.6f, w, 50f * unit), Spaced("CHAMBER COMPLETE"), big, Color.white, textAlpha, unit);
                    Label(new Rect(0f, h * 0.6f + 58f * unit, w, 30f * unit),
                        $"TIME  {SessionHud.FormatTime(endTime)}        DEATHS  {endDeaths}", small, Color.white, textAlpha, unit);
                }
                if (mode != Mode.Leaving && modeTime > PromptDelay)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin((modeTime - PromptDelay) * 2.5f);
                    Label(new Rect(0f, h - 70f * unit, w, 30f * unit),
                        end ? "PRESS  SPACE  TO  PLAY  AGAIN" : "CLICK  OR  PRESS  SPACE  TO  BEGIN", prompt, Color.white, pulse * textAlpha, unit);
                }
            }

            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        /// <summary>Text with a soft drop shadow, so it reads over bright rooms as well as dark ones.</summary>
        static void Label(Rect rect, string text, GUIStyle style, Color color, float alpha, float unit)
        {
            float o = Mathf.Max(1f, 2f * unit);
            GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
            GUI.Label(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, style);
        }

        void Styles(float unit)
        {
            if (styledFor == Screen.height && huge != null)
                return;
            styledFor = Screen.height;
            GUIStyle Make(float size) => new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(size * unit),
                normal = { textColor = Color.white },
            };
            small = Make(20f);
            prompt = Make(18f);
            big = Make(40f);
            huge = Make(64f);
        }

        static string Spaced(string text) => string.Join(" ", text.ToCharArray()).Replace("   ", "     ");

        /// <summary>Transparent at the top, dark at the bottom.</summary>
        static Texture2D Gradient()
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 64; y++)
            {
                float t = 1f - y / 63f;
                texture.SetPixel(0, y, new Color(0f, 0f, 0f, 0.8f * t * t));
            }
            texture.Apply();
            return texture;
        }
    }
}
