using UnityEngine;

namespace GravBoots.Hazards
{
    /// <summary>
    /// A piece of wreckage that flies, tumbles, bounces and comes to rest. Its own tiny
    /// simulation (a sphere cast per step against level geometry) rather than a rigidbody, so it
    /// never collides with or blocks the player and needs no physics layer setup.
    /// </summary>
    public class DebrisChunk : MonoBehaviour
    {
        [SerializeField] float radius = 0.1f;
        [SerializeField] float bounce = 0.3f;
        [SerializeField] float friction = 0.6f;
        [Tooltip("Seconds before it shrinks away; 0 keeps it as rubble.")]
        [SerializeField] float lifetime;

        Vector3 velocity, spin;
        float age;
        bool resting;
        Vector3 startScale;
        readonly RaycastHit[] hits = new RaycastHit[8];

        public bool IsResting => resting;

        /// <summary>A new cube of wreckage.</summary>
        public static DebrisChunk Spawn(string name, Vector3 position, Quaternion rotation, Vector3 size, Material material,
            Vector3 velocity, Vector3 spin, float lifetime = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // Immediately: a collider left until the end of the frame would block the player.
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var chunk = go.AddComponent<DebrisChunk>();
            chunk.radius = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f;
            chunk.lifetime = lifetime;
            chunk.Launch(velocity, spin);
            return chunk;
        }

        /// <summary>Sends an existing object (e.g. the sentry's head) flying as wreckage.</summary>
        public static DebrisChunk Throw(Transform piece, float radius, Vector3 velocity, Vector3 spin)
        {
            piece.SetParent(null, true);
            var chunk = piece.gameObject.AddComponent<DebrisChunk>();
            chunk.radius = radius;
            chunk.Launch(velocity, spin);
            return chunk;
        }

        void Launch(Vector3 initialVelocity, Vector3 initialSpin)
        {
            velocity = initialVelocity;
            spin = initialSpin;
            startScale = transform.localScale;
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float deltaTime)
        {
            age += deltaTime;
            if (lifetime > 0f)
            {
                float left = lifetime - age;
                if (left <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.localScale = startScale * Mathf.Clamp01(left / Mathf.Min(0.3f, lifetime));
            }
            if (resting)
                return;

            velocity += Physics.gravity * deltaTime;
            Vector3 move = velocity * deltaTime;
            float distance = move.magnitude;
            Vector3 position = transform.position;
            if (distance > 1e-5f && NearestHit(position, move / distance, distance, out RaycastHit hit))
            {
                position += move / distance * hit.distance + hit.normal * 0.005f;
                Vector3 normalSpeed = Vector3.Project(velocity, hit.normal);
                Vector3 slide = velocity - normalSpeed;
                velocity = -normalSpeed * bounce + slide * (1f - friction);
                spin *= 0.5f;
                if (velocity.magnitude < 0.6f && Vector3.Dot(hit.normal, -Physics.gravity.normalized) > 0.5f)
                    resting = true;
            }
            else
                position += move;

            transform.position = position;
            transform.rotation = Quaternion.Euler(spin * deltaTime) * transform.rotation;
            // Give up on anything that fell out of the world.
            if (age > 15f)
                resting = true;
        }

        bool NearestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int mask = ~(1 << Layers.Player);
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // Zero-distance hits are things it already overlaps (where it was spawned).
                if (hits[i].distance <= 0f || hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                nearest = hits[i];
            }
            return best < float.MaxValue;
        }
    }
}
