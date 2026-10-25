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

        CinemachineThirdPersonFollow follow;
        Camera view;
        Quaternion frame = Quaternion.identity;
        float pitch = 10f;
        Vector3 settleCandidate = Vector3.up;
        Vector3 settledUp = Vector3.up;
        float settleTimer;
        bool cursorLocked;

        public CameraUpMode Mode { get => mode; set => mode = value; }
        public CameraSettings Settings => settings;
        /// <summary>The camera's up, without pitch.</summary>
        public Vector3 Up => frame * Vector3.up;
        public Transform ViewTransform => view != null ? view.transform : pivot;

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
            Place();
            if (virtualCamera != null)
                virtualCamera.PreviousStateIsValid = false;
        }

        void LateUpdate()
        {
            if (target == null || settings == null)
                return;
            float deltaTime = Time.deltaTime;

            UpdateCursor();
            FollowUp(DesiredUp(target.up, deltaTime), deltaTime);
            ApplyLook(deltaTime);
            Place();
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

        void ApplySettings()
        {
            if (follow != null)
            {
                follow.ShoulderOffset = settings.shoulderOffset;
                follow.VerticalArmLength = settings.verticalArmLength;
                follow.CameraDistance = settings.cameraDistance;
                follow.Damping = settings.damping;
                follow.AvoidObstacles.CameraRadius = settings.collisionRadius;
            }
            if (virtualCamera != null)
                virtualCamera.Lens.FieldOfView = settings.fieldOfView;
        }

        void UpdateCursor()
        {
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
