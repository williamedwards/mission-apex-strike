using GravBoots.Gravity;
using GravBoots.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravBoots.Cameras
{
    /// <summary>How the camera's up follows the player's up (docs/PLAN.md §1.4).</summary>
    public enum CameraUpMode
    {
        /// <summary>Camera up equals player up, smoothed. On a ceiling the world is upside down.</summary>
        FullFollow,
        /// <summary>The camera rolls at most <see cref="CameraSettings.partialMaxRollDegrees"/> from world up.</summary>
        PartialFollow,
        /// <summary>Like full follow, but only once the player's up has stayed put for a moment.</summary>
        RecenterOnSettle,
    }

    /// <summary>
    /// Orbit rig for the over-the-shoulder camera. This transform carries the camera's up and
    /// heading (it is the Cinemachine brain's world-up override); the <see cref="pivot"/> child
    /// adds pitch and is the Cinemachine follow target.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.CameraRig)]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Transform pivot;
        [SerializeField] PlayerInputReader input;
        [SerializeField] CameraSettings settings;
        [SerializeField] CinemachineCamera virtualCamera;
        [SerializeField] CameraUpMode mode;
        [Tooltip("What the camera keeps clear of. Exclude the player.")]
        [SerializeField] LayerMask obstacleMask = ~0;

        CinemachineThirdPersonFollow follow;
        Camera view;
        Quaternion frame = Quaternion.identity;
        float pitch = 10f;
        Vector3 settleCandidate = Vector3.up;
        Vector3 settledUp = Vector3.up;
        float settleTimer;
        bool cursorLocked;
        float shoulderScale = 1f;
        float distance;
        bool holding;
        Vector3 heldPosition;

        public CameraUpMode Mode { get => mode; set => mode = value; }
        public CameraSettings Settings => settings;
        /// <summary>The camera's up, without pitch.</summary>
        public Vector3 Up => frame * Vector3.up;
        public Transform ViewTransform => view != null ? view.transform : pivot;
        /// <summary>Pitch pivot and Cinemachine follow target.</summary>
        public Transform Pivot => pivot;
        /// <summary>Frame of the last deliberate cut (spawn, teleport); a jump then is expected.</summary>
        public int LastSnapFrame { get; private set; } = -1;
        /// <summary>Current camera distance after obstacle avoidance (debug readout).</summary>
        public float CurrentDistance => distance;
        /// <summary>Current shoulder offset scale after obstacle avoidance (debug readout).</summary>
        public float ShoulderScale => shoulderScale;
        /// <summary>Where the rig wants the end of the shoulder arm, before Cinemachine damping.</summary>
        public Vector3 HandPosition => pivot.position + HandOffset(shoulderScale);
        /// <summary>Where the rig wants the camera, before Cinemachine damping.</summary>
        public Vector3 CameraPosition => holding ? heldPosition : HandPosition - pivot.rotation * Vector3.forward * distance;
        /// <summary>The camera has stopped following (see <see cref="Hold"/>).</summary>
        public bool Holding => holding;
        /// <summary>While holding: looking from where the camera stopped at the player's chest.</summary>
        public Quaternion HeldRotation =>
            Quaternion.LookRotation(target.position + target.up * 1.2f - heldPosition, Up);

        /// <summary>
        /// Stops following: the camera stays where it is and turns to keep the player in view, so
        /// an exit elevator carries them up and away from it. For the end of a run.
        /// </summary>
        public void Hold()
        {
            if (holding || target == null)
                return;
            // Where the rendered camera actually is (after Cinemachine's damping), if there is one.
            heldPosition = virtualCamera != null && view != null ? view.transform.position : CameraPosition;
            holding = true;
            if (follow != null)
                follow.enabled = false;
        }

        /// <summary>Wires the rig up from code (tests, tools); scenes set these in the Inspector.</summary>
        public void Configure(Transform target, Transform pivot, CameraSettings settings, LayerMask obstacleMask)
        {
            this.target = target;
            this.pivot = pivot;
            this.settings = settings;
            this.obstacleMask = obstacleMask;
        }

        void Start()
        {
            view = Camera.main;
            if (settings == null)
                Debug.LogError("CameraRig has no CameraSettings assigned; the camera will not follow the player.", this);
            if (virtualCamera != null)
                follow = virtualCamera.GetComponent<CinemachineThirdPersonFollow>();
            SnapBehindTarget();
            SetCursorLocked(true);
        }

        /// <summary>Puts the camera straight behind the target with no smoothing (spawn, teleport).</summary>
        public void SnapBehindTarget()
        {
            if (target == null)
                return;
            frame = SurfaceAlignment.AlignToSurface(target.forward, target.up);
            settleCandidate = settledUp = target.up;
            settleTimer = 0f;
            pitch = 10f;
            LastSnapFrame = Time.frameCount;
            Place();
            if (settings != null)
            {
                // Start from where obstacles allow, not from a stale value in another room.
                shoulderScale = ClearFraction(pivot.position, HandOffset(1f), settings.collisionRadius + settings.shoulderMargin);
                distance = ClearDistance(pivot.position + HandOffset(shoulderScale), settings.cameraDistance);
            }
            if (virtualCamera != null)
                virtualCamera.PreviousStateIsValid = false;
        }

        void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>Advances the rig by one frame. Called from LateUpdate; tests call it directly.</summary>
        public void Tick(float deltaTime)
        {
            if (target == null || settings == null)
                return;
            if (holding)
            {
                if (virtualCamera != null)
                    virtualCamera.transform.SetPositionAndRotation(heldPosition, HeldRotation);
                return;
            }

            UpdateCursor();
            FollowUp(DesiredUp(target.up, deltaTime), deltaTime);
            ApplyLook(deltaTime);
            Place();
            AvoidObstacles(deltaTime);
            ApplySettings();
        }

        Vector3 DesiredUp(Vector3 playerUp, float deltaTime)
        {
            switch (mode)
            {
                case CameraUpMode.PartialFollow:
                {
                    Quaternion world = SurfaceAlignment.AlignToSurface(frame * Vector3.forward, Vector3.up);
                    return SurfaceAlignment.RotateUpToward(world, playerUp, settings.partialMaxRollDegrees) * Vector3.up;
                }
                case CameraUpMode.RecenterOnSettle:
                    if (Vector3.Angle(playerUp, settleCandidate) > 2f)
                    {
                        settleCandidate = playerUp;
                        settleTimer = 0f;
                    }
                    else
                    {
                        settleTimer += deltaTime;
                    }
                    if (settleTimer >= settings.settleTime)
                        settledUp = settleCandidate;
                    return settledUp;
                default:
                    return playerUp;
            }
        }

        void FollowUp(Vector3 desiredUp, float deltaTime)
        {
            float angle = Vector3.Angle(Up, desiredUp);
            if (angle < 0.01f)
                return;
            float step = Mathf.Max(angle * (1f - Mathf.Exp(-settings.upSharpness * deltaTime)),
                settings.minUpDegreesPerSecond * deltaTime);
            // Turning the whole frame carries the heading along with the up, so the camera stays
            // behind the player through a transition instead of spinning.
            frame = SurfaceAlignment.RotateUpToward(frame, desiredUp, step);
        }

        void ApplyLook(float deltaTime)
        {
            if (input == null)
                return;
            Vector2 look = cursorLocked ? input.LookDelta * settings.mouseDegreesPerPixel : Vector2.zero;
            look += input.LookStick * (settings.stickDegreesPerSecond * deltaTime);
            if (settings.invertY)
                look.y = -look.y;

            frame = Quaternion.AngleAxis(look.x, Up) * frame;
            pitch = Mathf.Clamp(pitch - look.y, settings.minPitch, settings.maxPitch);
        }

        void Place()
        {
            float height = settings != null ? settings.pivotHeight : 1.5f;
            transform.SetPositionAndRotation(target.position + target.up * height, frame);
            if (pivot != null)
                pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// Keeps the shoulder and camera out of walls without sudden jumps.
        /// </summary>
        /// <remarks>
        /// Cinemachine's own avoidance snaps the camera in on the frame something gets in the
        /// way, and never damps the shoulder at all. Here both still track slow, continuous
        /// changes exactly (backing into a wall stays tight), but sudden changes (a pillar
        /// passing behind, the lip of a protruding block) are limited to a maximum speed, and
        /// moving back out is eased. The shoulder is probed with a margin, so it starts tucking
        /// in before it touches a wall.
        /// </remarks>
        void AvoidObstacles(float deltaTime)
        {
            Vector3 root = pivot.position;

            float shoulderTarget = ClearFraction(root, HandOffset(1f), settings.collisionRadius + settings.shoulderMargin);
            shoulderScale = shoulderTarget < shoulderScale
                ? Mathf.MoveTowards(shoulderScale, shoulderTarget, settings.shoulderRetractPerSecond * deltaTime)
                : Ease(shoulderScale, shoulderTarget, settings.easeOutTime, deltaTime);

            float distanceTarget = ClearDistance(root + HandOffset(shoulderScale), settings.cameraDistance);
            distance = distanceTarget < distance
                ? Mathf.MoveTowards(distance, distanceTarget, settings.pullInSpeed * deltaTime)
                : Ease(distance, distanceTarget, settings.easeOutTime, deltaTime);
        }

        /// <summary>Hand (end of the shoulder arm) relative to the pivot, with the arm scaled.</summary>
        Vector3 HandOffset(float scale)
        {
            return frame * (settings.shoulderOffset * scale) + pivot.rotation * new Vector3(0f, settings.verticalArmLength * scale, 0f);
        }

        /// <summary>Fraction (0..1) of <paramref name="offset"/> a sphere can travel from <paramref name="from"/>.</summary>
        float ClearFraction(Vector3 from, Vector3 offset, float radius)
        {
            float length = offset.magnitude;
            if (length < 1e-4f)
                return 1f;
            return Mathf.Clamp01(ClearTravel(from, offset / length, length, radius) / length);
        }

        /// <summary>How far behind <paramref name="hand"/> the camera can sit, up to <paramref name="wanted"/>.</summary>
        float ClearDistance(Vector3 hand, float wanted)
        {
            Vector3 back = -(pivot.rotation * Vector3.forward);
            return ClearTravel(hand, back, wanted, settings.collisionRadius);
        }

        /// <summary>
        /// How far a sphere of <paramref name="radius"/> can move from <paramref name="from"/>
        /// along <paramref name="direction"/>, up to <paramref name="length"/>.
        /// </summary>
        /// <remarks>
        /// A sphere cast ignores colliders it already overlaps at the start, which is the normal
        /// case when the player hugs a wall: the probe would see nothing and put the shoulder
        /// inside the wall. When the start overlaps, fall back to a ray and keep the radius
        /// clear of whatever it hits.
        /// </remarks>
        float ClearTravel(Vector3 from, Vector3 direction, float length, float radius)
        {
            if (Physics.CheckSphere(from, radius, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return Physics.Raycast(from, direction, out RaycastHit rayHit, length + radius, obstacleMask, QueryTriggerInteraction.Ignore)
                    ? Mathf.Max(0f, rayHit.distance - radius)
                    : length;
            }
            return Physics.SphereCast(from, radius, direction, out RaycastHit hit, length, obstacleMask, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : length;
        }

        static float Ease(float current, float target, float time, float deltaTime)
        {
            return time <= 0f ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / time));
        }

        void ApplySettings()
        {
            if (follow != null)
            {
                bool smooth = settings.smoothObstacleAvoidance;
                follow.ShoulderOffset = settings.shoulderOffset * (smooth ? shoulderScale : 1f);
                follow.VerticalArmLength = settings.verticalArmLength * (smooth ? shoulderScale : 1f);
                follow.CameraDistance = smooth ? distance : settings.cameraDistance;
                follow.Damping = settings.damping;
                follow.AvoidObstacles.Enabled = !smooth;
                follow.AvoidObstacles.CollisionFilter = obstacleMask;
                follow.AvoidObstacles.CameraRadius = settings.collisionRadius;
            }
            if (virtualCamera != null)
                virtualCamera.Lens.FieldOfView = settings.fieldOfView;
        }

        void UpdateCursor()
        {
            // A browser releases the pointer itself (Esc), without telling us.
            if (cursorLocked && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(false);
            if (input != null && input.PausePressed)
                SetCursorLocked(false);
            else if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
