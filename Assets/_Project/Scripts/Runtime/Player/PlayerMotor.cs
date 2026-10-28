using System;
using GravBoots.Gravity;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// Kinematic capsule controller that works for any Up direction (docs/PLAN.md §1.1).
    /// </summary>
    /// <remarks>
    /// All movement is expressed relative to <see cref="GravityBody.Up"/>. Nothing here may
    /// assume world up. The motor keeps its own pose and only writes it to the Rigidbody at
    /// the end of a step, so it can be stepped by hand (<see cref="Tick"/>) in tests.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(GravityBody))]
    [DefaultExecutionOrder(ExecutionOrder.Motor)]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] MotorSettings settings;
        [SerializeField] LayerMask collisionMask = ~0;
        [Tooltip("When off, the motor only advances when Tick is called (used by tests).")]
        [SerializeField] bool autoSimulate = true;

        Rigidbody body;
        CapsuleCollider capsule;
        GravityBody gravity;

        Vector3 position;
        Quaternion rotation;
        Vector3 velocity;
        Vector3 moveInput;
        float jumpBufferTimer;
        float coyoteTimer;

        // What we're standing on, and where we stand on it, so we move when it moves.
        Transform platform;
        Vector3 platformLocalPosition;
        Quaternion platformLocalRotation;
        Vector3 platformVelocity;
        bool justBoarded;

        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly Collider[] overlaps = new Collider[16];
        readonly Collider[] sideStepOverlaps = new Collider[16];

        public MotorSettings Settings { get => settings; set => settings = value; }
        public LayerMask CollisionMask { get => collisionMask; set => collisionMask = value; }
        public bool AutoSimulate { get => autoSimulate; set => autoSimulate = value; }
        public GravityBody Gravity => gravity;

        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
        public Vector3 Velocity => velocity;
        public Vector3 BodyUp => rotation * Vector3.up;
        public Vector3 MoveInput => moveInput;

        public bool IsGrounded { get; private set; }
        /// <summary>Contact normal of the ground (blended at edges), or Up when not grounded.</summary>
        public Vector3 GroundNormal { get; private set; }
        public Collider GroundCollider { get; private set; }
        /// <summary>Touching something underfoot that is too steep to stand on.</summary>
        public bool OnSteepSurface { get; private set; }
        /// <summary>Velocity of the point we stand on, from its motion over the last step (zero on static ground).</summary>
        public Vector3 PlatformVelocity => platformVelocity;

        /// <summary>Raised at the end of every step with its delta time, after the pose is final.</summary>
        public event Action<float> Stepped;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            gravity = GetComponent<GravityBody>();

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            position = transform.position;
            rotation = transform.rotation;
            GroundNormal = BodyUp;
        }

        void FixedUpdate()
        {
            if (autoSimulate)
                Tick(Time.fixedDeltaTime);
        }

        /// <summary>World-space desired move direction; magnitude 0..1 is the speed fraction.</summary>
        public void SetMoveInput(Vector3 worldDirection) => moveInput = Vector3.ClampMagnitude(worldDirection, 1f);

        public void RequestJump() => jumpBufferTimer = settings != null ? settings.jumpBufferTime : 0f;

        public void AddVelocity(Vector3 change) => velocity += change;

        public void Teleport(Vector3 newPosition, Quaternion newRotation)
        {
            position = newPosition;
            rotation = newRotation;
            velocity = Vector3.zero;
            IsGrounded = false;
            OnSteepSurface = false;
            GroundNormal = BodyUp;
            platform = null;
            platformVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        public void Tick(float deltaTime)
        {
            if (settings == null || deltaTime <= 0f)
                return;

            RideGround(deltaTime);
            gravity.Tick(deltaTime, rotation * Vector3.forward);
            Vector3 up = gravity.Up;

            UpdateOrientation(up, deltaTime);
            Depenetrate();

            bool wasGrounded = IsGrounded;
            bool jumped = UpdateVelocity(up, deltaTime);
            CollideAndSlide(velocity * deltaTime, up, wasGrounded && !jumped);
            UpdateGround(up, wasGrounded && !jumped);
            Depenetrate();
            RememberGround();

            if (autoSimulate)
            {
                body.MovePosition(position);
                body.MoveRotation(rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
                body.position = position;
                body.rotation = rotation;
            }

            Stepped?.Invoke(deltaTime);
        }

        /// <summary>
        /// Moves us with whatever we stood on last step, if it moved: a moving platform, or a
        /// sentry's head on a track. Static ground doesn't move, so this costs nothing there.
        /// </summary>
        void RideGround(float deltaTime)
        {
            if (platform == null)
            {
                platformVelocity = Vector3.zero;
                return;
            }
            Vector3 carried = platform.TransformPoint(platformLocalPosition);
            Quaternion carriedRotation = platform.rotation * platformLocalRotation;
            Quaternion turn = carriedRotation * Quaternion.Inverse(rotation);
            platformVelocity = (carried - position) / deltaTime;
            position = carried;
            rotation = carriedRotation;
            if (gravity.FollowsSurface)
                gravity.Rotate(turn);
            if (justBoarded)
            {
                // Landing on a moving platform: our own velocity is relative to it from now on.
                velocity -= Vector3.ProjectOnPlane(platformVelocity, GroundNormal);
                justBoarded = false;
            }
        }

        /// <summary>After the step: note what we stand on; leaving a moving platform keeps its velocity.</summary>
        void RememberGround()
        {
            Transform ground = IsGrounded && GroundCollider != null ? GroundCollider.transform : null;
            if (ground != platform)
            {
                if (ground == null)
                    velocity += platformVelocity;
                justBoarded = ground != null;
                platform = ground;
            }
            if (platform != null)
            {
                platformLocalPosition = platform.InverseTransformPoint(position);
                platformLocalRotation = Quaternion.Inverse(platform.rotation) * rotation;
            }
        }

        void UpdateOrientation(Vector3 up, float deltaTime)
        {
            Quaternion target = SurfaceAlignment.RotateUpToward(rotation, up, settings.alignDegreesPerSecond * deltaTime);

            Vector3 bodyUp = target * Vector3.up;
            Vector3 facing = Vector3.ProjectOnPlane(moveInput, bodyUp);
            if (facing.sqrMagnitude > 1e-4f)
            {
                target = Quaternion.RotateTowards(target, Quaternion.LookRotation(facing, bodyUp),
                    settings.turnDegreesPerSecond * deltaTime);
            }

            // Pivot about the capsule centre so turning over never swings the feet through geometry.
            Vector3 centre = position + rotation * capsule.center;
            rotation = target;
            position = centre - rotation * capsule.center;
        }

        /// <returns>True when a jump started this step.</returns>
        bool UpdateVelocity(Vector3 up, float deltaTime)
        {
            coyoteTimer = IsGrounded ? settings.coyoteTime : coyoteTimer - deltaTime;
            jumpBufferTimer -= deltaTime;

            Vector3 desired = Vector3.ProjectOnPlane(moveInput, up);
            desired = desired.sqrMagnitude > 1e-6f
                ? desired.normalized * (moveInput.magnitude * settings.moveSpeed)
                : Vector3.zero;

            if (IsGrounded)
            {
                Vector3 target = TangentToSurface(desired, GroundNormal, up);
                Vector3 current = Vector3.ProjectOnPlane(velocity, GroundNormal);
                velocity = Vector3.MoveTowards(current, target, settings.groundAcceleration * deltaTime);
            }
            else
            {
                float vertical = Vector3.Dot(velocity, up);
                Vector3 lateral = velocity - up * vertical;
                lateral = Vector3.MoveTowards(lateral, desired, settings.airAcceleration * deltaTime);
                vertical = Mathf.Max(vertical - settings.gravity * deltaTime, -settings.maxFallSpeed);
                velocity = lateral + up * vertical;
            }

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                velocity = Vector3.ProjectOnPlane(velocity, up) + up * settings.JumpSpeed;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// <paramref name="direction"/> (in the plane of up) bent onto the surface, keeping its length.
        /// </summary>
        static Vector3 TangentToSurface(Vector3 direction, Vector3 surfaceNormal, Vector3 up)
        {
            float length = direction.magnitude;
            if (length < 1e-6f)
                return Vector3.zero;
            Vector3 right = Vector3.Cross(direction, up);
            return Vector3.Cross(surfaceNormal, right).normalized * length;
        }

        void CollideAndSlide(Vector3 displacement, Vector3 up, bool canStep)
        {
            Vector3 remaining = displacement;
            for (int i = 0; i < settings.maxSlideIterations; i++)
            {
                float distance = remaining.magnitude;
                if (distance < 1e-5f)
                    break;

                Vector3 direction = remaining / distance;
                if (!CastCapsule(position, direction, distance + settings.skinWidth, out RaycastHit hit))
                {
                    position += remaining;
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - settings.skinWidth);
                position += direction * travel;
                remaining = direction * (distance - travel);

                bool steep = Vector3.Angle(hit.normal, up) > settings.slopeLimit;
                if (steep && canStep && TryStepUp(remaining, up))
                    break;

                Vector3 normal = steep ? ObstructionNormal(hit.normal, remaining, up) : hit.normal;
                remaining = Vector3.ProjectOnPlane(remaining, normal);
                if (Vector3.Dot(velocity, normal) < 0f)
                    velocity = Vector3.ProjectOnPlane(velocity, normal);
            }
        }

        /// <summary>
        /// Surfaces too steep to stand on must never lift the player: sliding along them would
        /// let you creep up a 60° ramp. If the slide would end up moving us up, treat the surface
        /// as a vertical wall instead.
        /// </summary>
        static Vector3 ObstructionNormal(Vector3 normal, Vector3 motion, Vector3 up)
        {
            Vector3 slid = Vector3.ProjectOnPlane(motion, normal);
            float slidUp = Vector3.Dot(slid, up);
            if (slidUp > 1e-5f && slidUp > Vector3.Dot(motion, up))
            {
                Vector3 wall = Vector3.ProjectOnPlane(normal, up);
                if (wall.sqrMagnitude > 1e-6f)
                    return wall.normalized;
            }
            return normal;
        }

        bool TryStepUp(Vector3 remaining, Vector3 up)
        {
            Vector3 lateral = Vector3.ProjectOnPlane(remaining, up);
            if (lateral.sqrMagnitude < 1e-8f)
                return false;
            Vector3 forward = lateral.normalized;
            // Go far enough over the lip that the capsule rests on it at a walkable angle;
            // a smaller nudge lands on footing the ground probe would then reject.
            float forwardDistance = Mathf.Max(lateral.magnitude, capsule.radius * 0.5f);
            float skin = settings.skinWidth;

            Vector3 p = position;
            float rise = settings.stepHeight;
            if (CastCapsule(p, up, rise + skin, out RaycastHit hit))
                rise = Mathf.Max(0f, hit.distance - skin);
            p += up * rise;

            if (CastCapsule(p, forward, forwardDistance + skin, out _))
                return false;
            p += forward * forwardDistance;

            if (!CastCapsule(p, -up, rise + skin * 2f, out hit))
                return false;
            Vector3 normal = FaceNormal(hit, up, forward * 0.02f);
            if (Vector3.Angle(normal, up) > settings.slopeLimit || Vector3.Angle(hit.normal, up) > settings.slopeLimit)
                return false;

            position = p - up * Mathf.Max(0f, hit.distance - skin);
            return true;
        }

        void UpdateGround(Vector3 up, bool snap)
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            OnSteepSurface = false;
            GroundCollider = null;
            GroundNormal = up;

            // Rising through the air (e.g. the start of a jump): don't grab the ground we just left.
            if (!wasGrounded && Vector3.Dot(velocity, up) > 0.01f)
                return;

            float skin = settings.skinWidth;
            GetCapsule(position, out _, out Vector3 bottom, out float radius);
            float castRadius = radius * 0.95f;
            float inset = radius - castRadius;
            float probe = (snap ? settings.groundSnapDistance : 0f) + skin * 2f;

            // Prefer the nearest walkable hit: at the foot of a steep ramp the probe touches both
            // the ramp and the floor, and the floor is what we stand on.
            int count = Physics.SphereCastNonAlloc(bottom, castRadius, -up, hits, probe + inset,
                collisionMask, QueryTriggerInteraction.Ignore);
            RaycastHit hit = default;
            Vector3 normal = up;
            float best = float.MaxValue;
            bool touchingSteep = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.collider == capsule || candidate.distance <= 0f)
                    continue;
                // Use the contact normal, not the face normal: on an edge it points from the edge
                // to the sphere's centre, which is exactly how stable that footing is. Resting
                // mostly over a ledge is walkable; touching the lip of a step in front is not.
                Vector3 contactNormal = candidate.normal;
                if (Vector3.Angle(contactNormal, up) > settings.slopeLimit)
                {
                    touchingSteep |= candidate.distance - inset <= skin * 2f;
                    continue;
                }
                if (candidate.distance < best)
                {
                    best = candidate.distance;
                    hit = candidate;
                    normal = contactNormal;
                }
            }
            if (best == float.MaxValue)
            {
                OnSteepSurface = touchingSteep;
                return;
            }

            float gap = hit.distance - inset;

            position -= up * (gap - skin);
            IsGrounded = true;
            GroundNormal = normal;
            GroundCollider = hit.collider;
            if (Vector3.Dot(velocity, normal) < 0f)
                velocity = Vector3.ProjectOnPlane(velocity, normal);
        }

        /// <summary>
        /// The normal of the face that was hit, sampled by a short ray near the contact.
        /// Sphere and capsule casts return a blend of normals at edges; this asks the face
        /// itself (e.g. whether the top of a step is walkable).
        /// </summary>
        static Vector3 FaceNormal(RaycastHit hit, Vector3 up, Vector3 nudge)
        {
            var ray = new Ray(hit.point + up * 0.1f + nudge, -up);
            return hit.collider.Raycast(ray, out RaycastHit faceHit, 0.25f) ? faceHit.normal : hit.normal;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with the colliders within <paramref name="margin"/> of
        /// the capsule, triggers included (our own collider left out), and returns how many.
        /// The motor rests a skin width off surfaces, so a margin of a few centimetres finds what
        /// the player is standing on or leaning against.
        /// </summary>
        public int Touching(float margin, Collider[] results)
        {
            GetCapsule(position, out Vector3 top, out Vector3 bottom, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius + margin, results, collisionMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (results[i] != capsule)
                    continue;
                results[i] = results[--count];
                break;
            }
            return count;
        }

        void Depenetrate()
        {
            for (int iteration = 0; iteration < 3; iteration++)
            {
                GetCapsule(position, out Vector3 top, out Vector3 bottom, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, collisionMask,
                    QueryTriggerInteraction.Ignore);

                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (other == capsule)
                        continue;
                    if (Physics.ComputePenetration(capsule, position, rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance))
                    {
                        // Pushed down into what we stand on (a platform coming down on us): step
                        // out from under it instead of being squeezed through the floor.
                        if (Vector3.Dot(direction, gravity.Up) < -0.3f && TrySideStep(other, out Vector3 sideStep))
                        {
                            position += sideStep;
                            moved = true;
                            continue;
                        }
                        position += direction * (distance + 0.001f);
                        if (Vector3.Dot(velocity, direction) < 0f)
                            velocity = Vector3.ProjectOnPlane(velocity, direction);
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
        }

        /// <summary>
        /// The shortest move across Up (in eight directions, out to 3 m) that clears
        /// <paramref name="other"/> without ending up inside anything else.
        /// </summary>
        bool TrySideStep(Collider other, out Vector3 offset)
        {
            offset = Vector3.zero;
            Vector3 up = gravity.Up;
            Vector3 across = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            if (across.sqrMagnitude < 1e-4f)
                across = Vector3.ProjectOnPlane(rotation * Vector3.right, up);
            across.Normalize();
            const float stepSize = 0.2f, maxDistance = 3f;
            for (float d = stepSize; d <= maxDistance + 1e-3f; d += stepSize)
            {
                for (int k = 0; k < 8; k++)
                {
                    Vector3 candidate = Quaternion.AngleAxis(45f * k, up) * across * d;
                    Vector3 at = position + candidate;
                    if (Physics.ComputePenetration(capsule, at, rotation, other, other.transform.position, other.transform.rotation, out _, out _))
                        continue;
                    if (OverlapsAnything(at))
                        continue;
                    offset = candidate;
                    return true;
                }
            }
            return false;
        }

        bool OverlapsAnything(Vector3 at)
        {
            GetCapsule(at, out Vector3 top, out Vector3 bottom, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius - settings.skinWidth, sideStepOverlaps, collisionMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (sideStepOverlaps[i] != capsule)
                    return true;
            return false;
        }

        void GetCapsule(Vector3 atPosition, out Vector3 top, out Vector3 bottom, out float radius)
        {
            Vector3 centre = atPosition + rotation * capsule.center;
            Vector3 axis = rotation * Vector3.up;
            radius = capsule.radius;
            float half = Mathf.Max(0f, capsule.height * 0.5f - radius);
            top = centre + axis * half;
            bottom = centre - axis * half;
        }

        bool CastCapsule(Vector3 from, Vector3 direction, float distance, out RaycastHit closest)
        {
            GetCapsule(from, out Vector3 top, out Vector3 bottom, out float radius);
            int count = Physics.CapsuleCastNonAlloc(top, bottom, radius, direction, hits, distance,
                collisionMask, QueryTriggerInteraction.Ignore);
            return Closest(count, out closest);
        }

        /// <summary>
        /// Nearest real hit, skipping our own collider and shapes we already overlap at the
        /// start of the cast (Depenetrate deals with those).
        /// </summary>
        bool Closest(int count, out RaycastHit closest)
        {
            closest = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == capsule || hit.distance <= 0f)
                    continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    closest = hit;
                }
            }
            return best < float.MaxValue;
        }
    }
}
