using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// Drives the character's Animator from the motor: speed along the body's surface, grounded,
    /// and a jump pose chosen by vertical speed. All measured relative to the body's own up, so
    /// it reads the same on floors, walls and ceilings. The clips play in place (no root motion).
    /// Magnetized, the gait is a heavy walk (no run) kept in stride with the ground. Dying plays a
    /// fall; respawning snaps back to standing.
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int MoveScaleId = Animator.StringToHash("MoveScale");
        public static readonly int GroundedId = Animator.StringToHash("Grounded");
        public static readonly int AirPhaseId = Animator.StringToHash("AirPhase");
        public static readonly int MagnetizedId = Animator.StringToHash("Magnetized");
        public static readonly int WalkScaleId = Animator.StringToHash("WalkScale");
        public static readonly int DeadId = Animator.StringToHash("Dead");

        [SerializeField] PlayerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] MagneticBoots boots;
        [SerializeField] Health health;
        [Tooltip("Ground speed (m/s) of the run clip; faster than this plays it faster.")]
        [SerializeField] float runClipSpeed = 3.5f;
        [Tooltip("Ground speed (m/s) of the magnetized walk clip; faster than this plays it faster, so planted feet keep pace with the ground.")]
        [SerializeField] float walkClipSpeed = 1.25f;
        [Tooltip("Seconds to ease the blend between idle, walk and run when speed changes.")]
        [SerializeField] float speedDamping = 0.12f;
        [Tooltip("Jump clip times (s): takeoff, peak and falling poses of its first hop.")]
        [SerializeField] Vector3 airPoseTimes = new Vector3(0.08f, 0.25f, 0.33f);
        [SerializeField] float jumpClipLength = 2.208f;
        [Tooltip("How long off the ground before the air pose shows, so small bumps don't flicker it.")]
        [SerializeField] float airDelay = 0.1f;

        float airTime;
        bool snapToStanding;

        public void Bind(PlayerMotor playerMotor, Animator playerAnimator, MagneticBoots playerBoots, Health playerHealth,
            float magnetizedWalkClipSpeed, float runClipGroundSpeed)
        {
            motor = playerMotor;
            animator = playerAnimator;
            boots = playerBoots;
            health = playerHealth;
            walkClipSpeed = magnetizedWalkClipSpeed;
            runClipSpeed = runClipGroundSpeed;
        }

        void OnEnable()
        {
            if (health == null)
                return;
            health.Died += OnDied;
            health.Restored += OnRestored;
        }

        void OnDisable()
        {
            if (health == null)
                return;
            health.Died -= OnDied;
            health.Restored -= OnRestored;
        }

        /// <summary>Falls over on any death: on the ground, shot in mid-air, or dropping into a pit.</summary>
        void OnDied(DeathCause cause)
        {
            if (animator != null)
                animator.SetBool(DeadId, true);
        }

        /// <summary>
        /// Respawned: back to standing without blending up from lying down. Done on the next
        /// update, because the session sets the boots for the checkpoint after restoring health.
        /// </summary>
        void OnRestored() => snapToStanding = true;

        void Update()
        {
            if (motor == null || animator == null)
                return;
            Vector3 up = motor.BodyUp;
            Vector3 velocity = motor.Velocity;
            animator.SetFloat(SpeedId, GroundSpeed(motor), speedDamping, Time.deltaTime);
            // Pace the clips by the eased speed the blend uses, so stride and blend agree.
            float speed = animator.GetFloat(SpeedId);
            animator.SetFloat(MoveScaleId, Mathf.Max(1f, speed / runClipSpeed));
            animator.SetFloat(WalkScaleId, Mathf.Max(1f, speed / walkClipSpeed));
            animator.SetBool(MagnetizedId, boots != null && boots.IsMagnetized);
            float vertical = Vector3.Dot(velocity, up);
            // A jump leaves the ground rising fast; show that at once, but let small drops pass.
            airTime = motor.IsGrounded ? 0f : airTime + Time.deltaTime;
            animator.SetBool(GroundedId, motor.IsGrounded || airTime < airDelay && vertical < 1f);
            float jumpSpeed = motor.Settings != null ? motor.Settings.JumpSpeed : 8f;
            animator.SetFloat(AirPhaseId, AirPoseTime(vertical, jumpSpeed, airPoseTimes) / jumpClipLength);

            if (snapToStanding)
            {
                snapToStanding = false;
                animator.SetBool(DeadId, false);
                animator.Play(boots != null && boots.IsMagnetized ? "MagnetizedMove" : "Move", 0, 0f);
            }
        }

        /// <summary>
        /// How fast the player moves along the surface under their own power. The motor's velocity
        /// is already relative to a platform being ridden, so standing on a moving one reads as 0.
        /// </summary>
        public static float GroundSpeed(PlayerMotor motor) => Vector3.ProjectOnPlane(motor.Velocity, motor.BodyUp).magnitude;

        /// <summary>
        /// Which jump-clip time to hold while airborne: the takeoff pose at full jump speed,
        /// the peak pose at the top, and the falling pose once dropping as fast as a jump rises.
        /// </summary>
        public static float AirPoseTime(float verticalSpeed, float jumpSpeed, Vector3 times)
        {
            float rise = jumpSpeed > 0f ? Mathf.Clamp(verticalSpeed / jumpSpeed, -1f, 1f) : 0f;
            return rise >= 0f ? Mathf.Lerp(times.y, times.x, rise) : Mathf.Lerp(times.y, times.z, -rise);
        }
    }
}
