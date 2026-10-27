using System;
using GravBoots.Player;
using UnityEngine;

namespace GravBoots.Hazards
{
    public enum SentryState { Idle, Lock, Charge, Fire, Cooldown, Disabled }

    /// <summary>
    /// A sentry robot that only sees and shoots in one flat plane: parallel to the surface it
    /// stands on, at <see cref="eyeHeight"/>. One hit kills. Dropping onto its head destroys it
    /// for good.
    /// </summary>
    /// <remarks>
    /// The plane makes orientation the counterplay: on the same surface you're exposed, on a
    /// wall you're only exposed where the plane crosses it (a red line drawn round the room
    /// shows where), and on the ceiling above you're safe and in position to drop on it.
    /// Idle until the player's body reaches into the plane, in range and line of sight,
    /// anywhere around it. Lock (turns to face you; it only charges once facing you, so being
    /// behind it buys time) → Charge (the sight flickers; for the last
    /// <see cref="aimLockTime"/> it holds its aim on the point it locked and turns white, so
    /// stepping aside or leaving the plane dodges) → Fire → Cooldown → Idle. A stomp sets off <see cref="SentryWreck"/>.
    /// A stomp needs the player landing on its head moving at least <see cref="stompSpeed"/>
    /// toward it, so walking onto it doesn't count. Drops fall to the world floor, so only
    /// floor sentries can be stomped. The stomp is checked on the player motor's
    /// <see cref="PlayerMotor.Stepped"/>; the rest runs in <see cref="Tick"/>, which is public
    /// so tests step it by hand.
    /// </remarks>
    public class Sentry : MonoBehaviour
    {
        [SerializeField] PlayerMotor target;
        [Tooltip("Turns to face the aim, and is thrown off when destroyed (visual only). Optional.")]
        [SerializeField] Transform head;
        [Tooltip("The solid body; crumbles into rubble when destroyed. Optional.")]
        [SerializeField] Transform body;
        [Tooltip("The searchlight and shot. Optional.")]
        [SerializeField] LineRenderer sight;
        [Tooltip("Traced round the room where the firing plane meets surfaces. Optional.")]
        [SerializeField] LineRenderer dangerLine;
        [SerializeField] Renderer eye;
        [SerializeField] Material eyeOff;
        [SerializeField] bool autoSimulate = true;

        [Header("Plane")]
        [SerializeField] float range = 14f;
        [Tooltip("Height of the firing plane above the surface the sentry stands on.")]
        [SerializeField] float eyeHeight = 1f;
        [Tooltip("Half the thickness of the plane it sees and shoots in.")]
        [SerializeField] float planeHalfThickness = 0.15f;

        [Header("Attack")]
        [Tooltip("Minimum time from noticing the player to charging.")]
        [SerializeField] float lockTime = 0.35f;
        [Tooltip("How fast it turns to face the player; it charges only once facing them.")]
        [SerializeField] float lockDegreesPerSecond = 270f;
        [SerializeField] float chargeTime = 0.6f;
        [SerializeField] float aimLockTime = 0.2f;
        [SerializeField] float fireTime = 0.15f;
        [SerializeField] float cooldownTime = 1f;
        [SerializeField] float loseSightGrace = 0.2f;
        [SerializeField] float beamRadius = 0.08f;

        [Header("Stomp")]
        [Tooltip("Top of the sentry's head above its surface.")]
        [SerializeField] float headTop = 1.3f;
        [SerializeField] float stompRadius = 0.75f;
        [Tooltip("How fast the player must be falling onto it (m/s). 5 m/s is a drop of about 0.5 m.")]
        [SerializeField] float stompSpeed = 5f;

        Health targetHealth;
        float capsuleHeight = 1.8f, capsuleRadius = 0.35f;
        Vector3 aim;
        Quaternion aimFrame;
        Vector3 lockedPoint;
        float targetDistance;
        Vector3 tracedPosition;
        Quaternion tracedRotation;
        float stateTime, unseenTime, recentFallSpeed, shotLength;
        bool initialised;
        readonly RaycastHit[] hits = new RaycastHit[16];
        const int DangerLineSegments = 96;

        public SentryState State { get; private set; }
        public bool IsDisabled => State == SentryState.Disabled;
        public bool AutoSimulate { get => autoSimulate; set => autoSimulate = value; }
        public PlayerMotor Target
        {
            get => target;
            set
            {
                Unsubscribe();
                target = value;
                CacheTarget();
                if (isActiveAndEnabled)
                    Subscribe();
            }
        }
        public Transform Body { get => body; set => body = value; }
        public Vector3 AimDirection => aim;
        public Vector3 Eye => transform.position + transform.up * eyeHeight;
        public float LockTime => lockTime;
        public float ChargeTime => chargeTime;
        public float AimLockTime => aimLockTime;

        /// <summary>Raised on each shot, with whether it hit.</summary>
        public event Action<bool> Fired;
        /// <summary>Raised once, when stomped.</summary>
        public event Action Destroyed;

        void Awake()
        {
            if (target == null)
                target = FindAnyObjectByType<PlayerMotor>();
            if (body == null)
                body = transform.Find("Body");
            CacheTarget();
            Initialise();
        }

        void OnEnable() => Subscribe();

        void OnDisable() => Unsubscribe();

        // The stomp is checked on every motor step, not once a frame: at a low frame rate the
        // motor can land on the head and slide off its edge between two frames.
        void Subscribe()
        {
            if (target != null)
                target.Stepped += OnTargetStepped;
        }

        void Unsubscribe()
        {
            if (target != null)
                target.Stepped -= OnTargetStepped;
        }

        void OnTargetStepped(float deltaTime)
        {
            if (State != SentryState.Disabled)
                CheckStomp(deltaTime);
        }

        void Start() => TraceDangerLine();

        void Update()
        {
            if (autoSimulate)
                Tick(Time.deltaTime);
            if (State != SentryState.Disabled
                && ((transform.position - tracedPosition).sqrMagnitude > 1e-4f || Quaternion.Angle(transform.rotation, tracedRotation) > 0.1f))
                TraceDangerLine();
            DrawSight();
        }

        void CacheTarget()
        {
            targetHealth = target != null ? target.GetComponent<Health>() : null;
            var capsule = target != null ? target.GetComponent<CapsuleCollider>() : null;
            if (capsule != null)
            {
                capsuleHeight = capsule.height;
                capsuleRadius = capsule.radius;
            }
        }

        void Initialise()
        {
            aim = transform.forward;
            aimFrame = transform.rotation;
            initialised = true;
        }

        public void Tick(float deltaTime)
        {
            if (!initialised)
                Initialise();
            if (State == SentryState.Disabled)
                return;
            // Riding a track: the aim turns with the sentry.
            aim = (transform.rotation * Quaternion.Inverse(aimFrame)) * aim;
            aimFrame = transform.rotation;

            stateTime += deltaTime;
            bool sees = CanSee(out Vector3 toTarget);
            unseenTime = sees ? 0f : unseenTime + deltaTime;
            bool lost = unseenTime > loseSightGrace;

            switch (State)
            {
                case SentryState.Idle:
                    if (sees)
                        SetState(SentryState.Lock);
                    break;
                case SentryState.Lock:
                    if (lost)
                        SetState(SentryState.Idle);
                    else
                    {
                        TurnToward(toTarget, sees, deltaTime);
                        if (stateTime >= lockTime && Vector3.Angle(aim, toTarget) < 3f)
                            SetState(SentryState.Charge);
                    }
                    break;
                case SentryState.Charge:
                    if (lost)
                        SetState(SentryState.Idle);
                    else
                    {
                        if (stateTime < chargeTime - aimLockTime)
                        {
                            TurnToward(toTarget, sees, deltaTime);
                            lockedPoint = Eye + aim * targetDistance;
                        }
                        else
                            AimAt(lockedPoint);
                        if (stateTime >= chargeTime)
                        {
                            SetState(SentryState.Fire);
                            Shoot();
                        }
                    }
                    break;
                case SentryState.Fire:
                    if (stateTime >= fireTime)
                        SetState(SentryState.Cooldown);
                    break;
                case SentryState.Cooldown:
                    if (stateTime >= cooldownTime)
                        SetState(SentryState.Idle);
                    break;
            }
            if (head != null)
                head.rotation = Quaternion.LookRotation(aim, transform.up);
        }

        void SetState(SentryState next)
        {
            State = next;
            stateTime = 0f;
        }

        /// <summary>
        /// Keeps aiming at the point locked in at the end of tracking. A point rather than a
        /// direction, so a sentry moving on a track still hits where it locked, while a player
        /// who steps aside is missed.
        /// </summary>
        void AimAt(Vector3 point)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - Eye, transform.up);
            if (direction.sqrMagnitude > 1e-4f)
                aim = direction.normalized;
        }

        void TurnToward(Vector3 toTarget, bool sees, float deltaTime)
        {
            if (sees)
                aim = Vector3.RotateTowards(aim, toTarget, lockDegreesPerSecond * Mathf.Deg2Rad * deltaTime, 0f).normalized;
        }

        /// <summary>Whether the player's body reaches into the plane, in range and in line of sight; <paramref name="toTarget"/> lies in the plane.</summary>
        bool CanSee(out Vector3 toTarget)
        {
            toTarget = aim;
            if (target == null || targetHealth != null && targetHealth.IsDead)
                return false;
            Vector3 eye = Eye;
            if (!BeamMath.CapsuleInSlab(target.Position, target.BodyUp, capsuleHeight, capsuleRadius,
                    eye, transform.up, planeHalfThickness, out Vector3 aimPoint))
                return false;
            Vector3 offset = aimPoint - eye;
            float distance = offset.magnitude;
            if (distance > range || distance < 0.01f)
                return false;
            toTarget = offset / distance;
            targetDistance = distance;
            return ObstacleDistance(eye, toTarget, distance) >= distance - capsuleRadius;
        }

        void Shoot()
        {
            Vector3 eye = Eye;
            shotLength = ObstacleDistance(eye, aim, range);
            bool hit = target != null && BeamMath.HitsCapsule(eye, eye + aim * shotLength, beamRadius,
                target.Position, target.BodyUp, capsuleHeight, capsuleRadius);
            if (hit)
                targetHealth?.Kill(DeathCause.Laser);
            Fired?.Invoke(hit);
        }

        /// <summary>Landing on its head, falling fast enough, destroys it.</summary>
        void CheckStomp(float deltaTime)
        {
            if (target == null || targetHealth != null && targetHealth.IsDead)
                return;
            Vector3 up = transform.up;
            float fallSpeed = -Vector3.Dot(target.Velocity, up);
            // Remembered briefly: the motor may already have stopped the fall on the landing step.
            recentFallSpeed = Mathf.Max(fallSpeed, recentFallSpeed - 60f * deltaTime);

            Vector3 local = transform.InverseTransformPoint(target.Position);
            bool overHead = new Vector2(local.x, local.z).magnitude <= stompRadius;
            bool atHead = local.y >= headTop - 0.2f && local.y <= headTop + 0.4f;
            bool upright = Vector3.Angle(target.BodyUp, up) < 30f;
            if (overHead && atHead && upright && recentFallSpeed >= stompSpeed)
                Disable();
        }

        /// <summary>Destroys the sentry for good (a stomp).</summary>
        public void Disable()
        {
            if (State == SentryState.Disabled)
                return;
            SetState(SentryState.Disabled);
            if (sight != null)
                sight.enabled = false;
            if (dangerLine != null)
                dangerLine.enabled = false;
            Material spark = eye != null ? eye.sharedMaterial : null;
            if (eye != null && eyeOff != null)
                eye.sharedMaterial = eyeOff;
            // A wreck stops dead, even on a track.
            foreach (var mover in GetComponentsInParent<GravBoots.Level.KinematicMover>())
                if (mover.platform == transform)
                    mover.enabled = false;
            SentryWreck.Break(this, body, head, spark, target != null ? target.Position : transform.position);
            Destroyed?.Invoke();
        }

        /// <summary>Distance to the nearest solid collider along a ray, ignoring the player and the sentry itself.</summary>
        float ObstacleDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            int mask = ~(1 << Layers.Player);
            int count = Physics.RaycastNonAlloc(origin, direction, hits, maxDistance, mask, QueryTriggerInteraction.Ignore);
            float nearest = maxDistance;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform))
                    continue;
                nearest = Mathf.Min(nearest, hits[i].distance);
            }
            return nearest;
        }

        /// <summary>Where the firing plane meets surfaces within range, as a loop of points.</summary>
        Vector3[] DangerLoop()
        {
            var points = new Vector3[DangerLineSegments];
            Vector3 eye = Eye;
            for (int i = 0; i < DangerLineSegments; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(360f * i / DangerLineSegments, transform.up) * transform.forward;
                points[i] = eye + direction * (ObstacleDistance(eye, direction, range) - 0.03f);
            }
            return points;
        }

        void TraceDangerLine()
        {
            if (dangerLine == null || State == SentryState.Disabled)
                return;
            tracedPosition = transform.position;
            tracedRotation = transform.rotation;
            Vector3[] points = DangerLoop();
            dangerLine.loop = true;
            dangerLine.positionCount = points.Length;
            dangerLine.SetPositions(points);
        }

        void DrawSight()
        {
            if (sight == null)
                return;
            bool visible = State is SentryState.Lock or SentryState.Charge or SentryState.Fire;
            sight.enabled = visible;
            if (!visible)
                return;

            float width;
            Color color;
            float length = State == SentryState.Fire ? shotLength : ObstacleDistance(Eye, aim, range);
            switch (State)
            {
                case SentryState.Lock:
                    width = 0.04f;
                    color = new Color(1f, 0.2f, 0.1f, 0.85f);
                    break;
                case SentryState.Charge:
                    float t = Mathf.Clamp01(stateTime / chargeTime);
                    bool locked = stateTime >= chargeTime - aimLockTime;
                    float flicker = Mathf.Sin(stateTime * Mathf.Lerp(25f, 80f, t)) > 0f ? 1f : 0.5f;
                    width = Mathf.Lerp(0.04f, 0.08f, t);
                    color = locked ? new Color(1f, 0.95f, 0.9f, 1f) : new Color(1f, 0.2f, 0.1f, flicker);
                    break;
                default:
                    width = 0.3f;
                    color = new Color(1f, 0.6f, 0.45f, 1f);
                    break;
            }
            Vector3 eye = Eye;
            sight.positionCount = 2;
            sight.SetPosition(0, eye);
            sight.SetPosition(1, eye + aim * length);
            sight.startWidth = sight.endWidth = width;
            sight.startColor = sight.endColor = color;
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;
            // The danger line, so level design can see where the plane reaches.
            Vector3[] points = DangerLoop();
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.7f);
            for (int i = 0; i < points.Length; i++)
                Gizmos.DrawLine(points[i], points[(i + 1) % points.Length]);
        }
    }
}
