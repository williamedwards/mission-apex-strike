using System;
using GravBoots.Player;
using UnityEngine;

namespace GravBoots.Hazards
{
    public enum TurretState { Idle, Track, Charge, Fire, Cooldown }

    /// <summary>
    /// Placeholder laser turret for the "Is it fun?" chamber (the Phase 4 turret, stripped
    /// down). Mounted on any surface: its up is the surface normal and it only sees the side in
    /// front of it.
    /// </summary>
    /// <remarks>
    /// Idle → Track (laser sight, turning toward the player) → Charge (the sight thickens and
    /// flickers; for the last <see cref="aimLockTime"/> the aim freezes and the sight turns
    /// white, so stepping aside dodges) → Fire (one hitscan beam) → Cooldown. Breaking line of
    /// sight for longer than <see cref="loseSightGrace"/> sends it back to Idle, so going round
    /// onto another surface is real counterplay. The hit test is geometric
    /// (<see cref="BeamMath.HitsCapsule"/>) against the motor's pose, so it doesn't depend on
    /// transforms being synced, and <see cref="Tick"/> is public so tests can step it by hand.
    /// </remarks>
    public class Turret : MonoBehaviour
    {
        [SerializeField] PlayerMotor target;
        [Tooltip("Turns to face the aim (visual only). Optional.")]
        [SerializeField] Transform head;
        [Tooltip("The laser sight and beam. Optional.")]
        [SerializeField] LineRenderer beam;
        [SerializeField] bool autoSimulate = true;

        [Header("Sight")]
        [SerializeField] float range = 30f;
        [Tooltip("Height of the eye above the mount surface.")]
        [SerializeField] float eyeHeight = 0.6f;
        [Tooltip("Aim at this height up the player's body.")]
        [SerializeField] float aimHeight = 1f;
        [SerializeField] float trackDegreesPerSecond = 150f;
        [SerializeField] float loseSightGrace = 0.3f;

        [Header("Timing")]
        [SerializeField] float lockOnTime = 0.4f;
        [SerializeField] float chargeTime = 0.8f;
        [Tooltip("The last part of the charge, during which the aim no longer follows the player.")]
        [SerializeField] float aimLockTime = 0.25f;
        [SerializeField] float fireTime = 0.12f;
        [SerializeField] float cooldownTime = 1.2f;

        [Header("Shot")]
        [SerializeField] float damage = 50f;
        [SerializeField] float beamRadius = 0.08f;

        Health targetHealth;
        float capsuleHeight = 1.8f, capsuleRadius = 0.35f;
        Quaternion aim;
        float stateTime, unseenTime, sightLength, shotLength;
        bool aimInitialised;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public TurretState State { get; private set; }
        public bool AutoSimulate { get => autoSimulate; set => autoSimulate = value; }
        public PlayerMotor Target { get => target; set { target = value; CacheTarget(); } }
        public Vector3 AimDirection => aim * Vector3.forward;
        public Vector3 Eye => transform.position + transform.up * eyeHeight;
        /// <summary>Seconds from first seeing the player to the first shot.</summary>
        public float TimeToFirstShot => lockOnTime + chargeTime;
        public float ChargeTime => chargeTime;
        public float AimLockTime => aimLockTime;

        /// <summary>Raised on each shot, with whether it hit.</summary>
        public event Action<bool> Fired;

        void Awake()
        {
            ResetAim();
            if (target == null)
                target = FindAnyObjectByType<PlayerMotor>();
            CacheTarget();
        }

        void Update()
        {
            if (autoSimulate)
                Tick(Time.deltaTime);
            DrawBeam();
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

        void ResetAim()
        {
            // Resting pose: looking out along the mount's forward.
            aim = transform.rotation;
            aimInitialised = true;
        }

        public void Tick(float deltaTime)
        {
            if (!aimInitialised)
                ResetAim();
            stateTime += deltaTime;
            bool sees = CanSee(out Vector3 toTarget);
            unseenTime = sees ? 0f : unseenTime + deltaTime;
            bool lost = unseenTime > loseSightGrace || targetHealth != null && targetHealth.IsDead;

            switch (State)
            {
                case TurretState.Idle:
                    if (sees && !lost)
                        SetState(TurretState.Track);
                    break;
                case TurretState.Track:
                    if (lost)
                        SetState(TurretState.Idle);
                    else
                    {
                        Follow(toTarget, sees, deltaTime);
                        if (stateTime >= lockOnTime)
                            SetState(TurretState.Charge);
                    }
                    break;
                case TurretState.Charge:
                    if (lost)
                        SetState(TurretState.Idle);
                    else
                    {
                        if (stateTime < chargeTime - aimLockTime)
                            Follow(toTarget, sees, deltaTime);
                        if (stateTime >= chargeTime)
                        {
                            SetState(TurretState.Fire);
                            Shoot();
                        }
                    }
                    break;
                case TurretState.Fire:
                    if (stateTime >= fireTime)
                        SetState(TurretState.Cooldown);
                    break;
                case TurretState.Cooldown:
                    if (stateTime >= cooldownTime)
                        SetState(sees && !lost ? TurretState.Track : TurretState.Idle);
                    break;
            }
            if (head != null)
                head.rotation = aim;
        }

        void SetState(TurretState next)
        {
            State = next;
            stateTime = 0f;
        }

        void Follow(Vector3 toTarget, bool sees, float deltaTime)
        {
            if (!sees)
                return;
            Vector3 mountUp = transform.up;
            Vector3 hint = Vector3.Cross(toTarget, mountUp).sqrMagnitude > 1e-6f ? mountUp : aim * Vector3.up;
            aim = Quaternion.RotateTowards(aim, Quaternion.LookRotation(toTarget, hint), trackDegreesPerSecond * deltaTime);
        }

        /// <summary>Whether the player's chest is in range, in front of the mount, and not behind anything.</summary>
        bool CanSee(out Vector3 toTarget)
        {
            toTarget = Vector3.zero;
            if (target == null || targetHealth != null && targetHealth.IsDead)
                return false;
            Vector3 eye = Eye;
            Vector3 aimPoint = target.Position + target.BodyUp * aimHeight;
            Vector3 offset = aimPoint - eye;
            float distance = offset.magnitude;
            if (distance > range || distance < 0.01f)
                return false;
            toTarget = offset / distance;
            if (Vector3.Dot(toTarget, transform.up) < -0.05f)
                return false;
            sightLength = distance;
            return ObstacleDistance(eye, toTarget, distance) >= distance - 0.05f;
        }

        void Shoot()
        {
            Vector3 eye = Eye, direction = AimDirection;
            shotLength = ObstacleDistance(eye, direction, range);
            bool hit = target != null && BeamMath.HitsCapsule(eye, eye + direction * shotLength, beamRadius,
                target.Position, target.BodyUp, capsuleHeight, capsuleRadius);
            if (hit)
                targetHealth?.Damage(damage);
            Fired?.Invoke(hit);
        }

        /// <summary>Distance to the nearest solid collider along a ray, ignoring the player and the turret itself.</summary>
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

        void DrawBeam()
        {
            if (beam == null)
                return;
            bool visible = State is TurretState.Track or TurretState.Charge or TurretState.Fire;
            beam.enabled = visible;
            if (!visible)
                return;

            float width;
            Color color;
            float length = sightLength;
            switch (State)
            {
                case TurretState.Track:
                    width = 0.02f;
                    color = new Color(1f, 0.15f, 0.1f, 0.35f);
                    break;
                case TurretState.Charge:
                    float t = Mathf.Clamp01(stateTime / chargeTime);
                    bool locked = stateTime >= chargeTime - aimLockTime;
                    float flicker = Mathf.Sin(stateTime * Mathf.Lerp(20f, 70f, t)) > 0f ? 1f : 0.55f;
                    width = Mathf.Lerp(0.03f, 0.07f, t);
                    color = locked ? new Color(1f, 0.95f, 0.9f, 1f) : new Color(1f, 0.15f, 0.1f, Mathf.Lerp(0.5f, 1f, t) * flicker);
                    length = ObstacleDistance(Eye, AimDirection, range);
                    break;
                default:
                    width = 0.25f;
                    color = new Color(1f, 0.6f, 0.45f, 1f);
                    length = shotLength;
                    break;
            }
            Vector3 eye = Eye;
            beam.positionCount = 2;
            beam.SetPosition(0, eye);
            beam.SetPosition(1, eye + AimDirection * length);
            beam.startWidth = beam.endWidth = width;
            beam.startColor = beam.endColor = color;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
            Vector3 eye = transform.position + transform.up * eyeHeight;
            Gizmos.DrawWireSphere(eye, 0.2f);
            Gizmos.DrawRay(eye, transform.forward * 3f);
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.12f);
            Gizmos.DrawWireSphere(eye, range);
        }
    }
}
