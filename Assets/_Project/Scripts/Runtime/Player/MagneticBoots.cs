using System;
using GravBoots.Gravity;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>What the boots are doing (docs/PLAN.md §1.3).</summary>
    public enum BootsState
    {
        /// <summary>Boots off; world gravity.</summary>
        Off,
        /// <summary>Stuck to a magnetic surface; Up follows it.</summary>
        Magnetized,
        /// <summary>Just let go: falling under world gravity and turning upright until landing.</summary>
        Dropping,
    }

    /// <summary>
    /// The magnetic boots. Decides the player's Up after each motor step: following the
    /// magnetic surface underfoot, and dropping back to world gravity.
    /// </summary>
    /// <remarks>
    /// Changing orientation only happens along curved magnetic surfaces (quarter-pipes, rounded
    /// edges, pipes), whose segments meet at small angles. A sharp join, such as a wall meeting a
    /// floor at 90°, is impassable: walking into it just blocks, and walking off a sharp edge
    /// loses contact and drops. That keeps every route visible in the level geometry.
    /// The boots only switch on while standing on a magnetic surface, never in mid-air, so a
    /// drop is a commitment (pillar 2). They run on the motor's <see cref="PlayerMotor.Stepped"/>
    /// event, so tests drive them by stepping the motor.
    /// </remarks>
    [RequireComponent(typeof(PlayerMotor))]
    public class MagneticBoots : MonoBehaviour
    {
        [SerializeField] BootsSettings settings;
        [SerializeField] PlayerInputReader input;

        PlayerMotor motor;
        GravityBody gravity;
        bool toggleRequested;
        float lostContactTime;

        public BootsSettings Settings { get => settings; set => settings = value; }
        public BootsState State { get; private set; }
        public bool IsMagnetized => State == BootsState.Magnetized;
        /// <summary>Raised whenever <see cref="State"/> changes, with the old and new state.</summary>
        public event Action<BootsState, BootsState> StateChanged;
        /// <summary>Raised when switching on is refused because there's no magnetic surface underfoot.</summary>
        public event Action ActivationRefused;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            gravity = motor.Gravity;
            if (input == null)
                input = GetComponent<PlayerInputReader>();
        }

        void OnEnable() => motor.Stepped += OnStepped;

        void OnDisable() => motor.Stepped -= OnStepped;

        void Update()
        {
            if (input != null && input.ToggleBootsPressed)
                RequestToggle();
        }

        /// <summary>Asks for the boots to switch on or off; applied after the next motor step.</summary>
        public void RequestToggle() => toggleRequested = true;

        /// <summary>
        /// Gives the boots first say over a jump press. Returns true when the boots handled it,
        /// in which case the caller must not jump (see <see cref="BootsSettings.jumpWhileMagnetized"/>).
        /// </summary>
        public bool HandleJump()
        {
            if (State != BootsState.Magnetized || settings == null)
                return false;
            if (settings.jumpWhileMagnetized == MagnetizedJump.LetsGo)
                RequestToggle();
            return true;
        }

        /// <summary>Switches the boots off without a drop (debug gravity keys, respawn).</summary>
        public void ForceOff()
        {
            toggleRequested = false;
            SetState(BootsState.Off);
        }

        /// <summary>
        /// Switches the boots on at once, without the grounded check (respawning onto a
        /// checkpoint on a magnetic surface). Contact is checked from the next step as usual.
        /// </summary>
        public void Attach()
        {
            toggleRequested = false;
            lostContactTime = 0f;
            SetState(BootsState.Magnetized);
        }

        void OnStepped(float deltaTime)
        {
            if (settings == null)
                return;

            bool toggle = toggleRequested;
            toggleRequested = false;

            switch (State)
            {
                case BootsState.Off:
                    if (toggle)
                        TryActivate();
                    break;

                case BootsState.Magnetized:
                    if (toggle)
                        Drop();
                    else
                        UpdateAttachment(deltaTime);
                    break;

                case BootsState.Dropping:
                    // No switching on in mid-air: a drop is a commitment.
                    if (motor.IsGrounded)
                        SetState(BootsState.Off);
                    break;
            }
        }

        void TryActivate()
        {
            if (!motor.IsGrounded || !Layers.IsMagnetic(motor.GroundCollider))
            {
                ActivationRefused?.Invoke();
                return;
            }
            lostContactTime = 0f;
            SetState(BootsState.Magnetized);
        }

        void UpdateAttachment(float deltaTime)
        {
            Vector3 up = gravity.Up;
            if (motor.IsGrounded)
            {
                if (TryGetSupport(up, out Vector3 normal))
                {
                    lostContactTime = 0f;
                    float angle = Vector3.Angle(up, normal);
                    if (angle > 0.1f)
                        gravity.BlendUp(normal, settings.followDegreesPerSecond);
                    return;
                }
                // Over a sharp edge, or standing on something non-magnetic.
                LoseContact(deltaTime);
                return;
            }

            LoseContact(deltaTime);
        }

        /// <summary>
        /// The magnetic face directly under the player's centre, if it's within
        /// <see cref="BootsSettings.followMaxAngle"/> of Up.
        /// </summary>
        /// <remarks>
        /// Deliberately a ray along -Up from the centre, not the motor's contact normal. Rolling
        /// over a sharp edge, the contact normal blends smoothly from one face to the other, which
        /// would let the boots follow it round a right angle. The face under the centre jumps
        /// instead: on a curve it's always a facet a few degrees away, but past a sharp edge it's
        /// empty space or a face at 90°, so contact is lost.
        /// </remarks>
        bool TryGetSupport(Vector3 up, out Vector3 normal)
        {
            normal = up;
            const float lift = 0.5f;
            Vector3 origin = motor.Position + up * lift;
            float length = lift + motor.Settings.groundSnapDistance + 0.1f;
            if (!Physics.Raycast(origin, -up, out RaycastHit hit, length, motor.CollisionMask, QueryTriggerInteraction.Ignore))
                return false;
            if (!Layers.IsMagnetic(hit.collider) || Vector3.Angle(up, hit.normal) > settings.followMaxAngle)
                return false;
            normal = hit.normal;
            return true;
        }

        void LoseContact(float deltaTime)
        {
            lostContactTime += deltaTime;
            if (lostContactTime > settings.contactGrace)
                Drop();
        }

        void Drop()
        {
            Vector3 oldUp = gravity.Up;
            gravity.SetUp(Vector3.up);
            if (Vector3.Angle(oldUp, Vector3.up) > 1f)
            {
                motor.AddVelocity(oldUp * settings.pushOffSpeed);
                SetState(BootsState.Dropping);
            }
            else
            {
                SetState(BootsState.Off);
            }
        }

        void SetState(BootsState next)
        {
            if (next == State)
                return;
            BootsState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
