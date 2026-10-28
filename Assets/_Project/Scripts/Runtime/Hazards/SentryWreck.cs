using UnityEngine;

namespace GravBoots.Hazards
{
    /// <summary>
    /// What a stomped sentry turns into: a flash and a burst of sparks, the head thrown off,
    /// and the body crumbling into a scatter of chunks round a low stump (which stays solid, so
    /// the player lands on it). Visual only: the sentry is already disabled.
    /// </summary>
    public static class SentryWreck
    {
        const float StumpHeight = 0.35f;
        /// <summary>Roughly how big each rubble chunk is; bigger bodies break into more of them.</summary>
        const float ChunkSize = 0.45f;
        const int Sparks = 14;

        /// <param name="rubble">The stump and chunks' material; null for the body's own.</param>
        /// <param name="stomper">Where the player was standing; the head is thrown away from it.</param>
        public static void Break(Sentry sentry, Transform body, Transform head, Material spark, Material rubble, Vector3 stomper)
        {
            Transform root = sentry.transform;
            Vector3 up = root.up;
            // Seeded by position, so a given sentry always breaks the same way.
            var random = new System.Random(root.position.GetHashCode());
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Vector3 Spin(float speed) => new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)) * speed;

            Vector3 centre = root.position + up * 1f;
            Flash(centre);

            if (body != null)
            {
                var renderer = body.GetComponentInChildren<Renderer>();
                Material shell = rubble != null ? rubble : renderer != null ? renderer.sharedMaterial : null;
                Bounds local = LocalBounds(body);
                body.gameObject.SetActive(false);

                var stump = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stump.name = "Stump";
                stump.transform.SetParent(root, false);
                stump.transform.localPosition = new Vector3(local.center.x, StumpHeight * 0.5f, local.center.z);
                stump.transform.localScale = new Vector3(local.size.x, StumpHeight, local.size.z);
                if (shell != null)
                    stump.GetComponent<MeshRenderer>().sharedMaterial = shell;

                // The rest of the body, cut into a grid of chunks that burst outward.
                int Count(float size) => Mathf.Clamp(Mathf.RoundToInt(size / ChunkSize), 2, 4);
                int chunksX = Count(local.size.x), chunksY = Count(local.max.y - StumpHeight) + 1, chunksZ = Count(local.size.z);
                Vector3 min = new Vector3(local.min.x, StumpHeight, local.min.z);
                Vector3 cell = new Vector3(local.size.x / chunksX, (local.max.y - StumpHeight) / chunksY, local.size.z / chunksZ);
                for (int x = 0; x < chunksX; x++)
                for (int y = 0; y < chunksY; y++)
                for (int z = 0; z < chunksZ; z++)
                {
                    Vector3 localCentre = min + Vector3.Scale(cell, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                    Vector3 outward = new Vector3(localCentre.x, 0f, localCentre.z).normalized;
                    Vector3 velocity = root.TransformDirection(outward * Range(1.5f, 3.5f) + Vector3.up * Range(0.5f, 2.5f + y));
                    DebrisChunk.Spawn("Rubble", root.TransformPoint(localCentre), root.rotation * Quaternion.Euler(Spin(20f)),
                        cell * Range(0.75f, 0.95f), shell, velocity, Spin(500f));
                }
            }

            if (head != null)
            {
                Vector3 away = Vector3.ProjectOnPlane(root.position - stomper, up);
                if (away.sqrMagnitude < 0.01f)
                    away = -root.forward;
                Vector3 velocity = away.normalized * Range(2.5f, 3.5f) + up * Range(3f, 4f);
                DebrisChunk.Throw(head, 0.25f, velocity, Spin(600f));
            }

            if (spark != null)
            {
                for (int i = 0; i < Sparks; i++)
                {
                    Vector3 direction = (RandomVector(random) + up * 0.8f).normalized;
                    DebrisChunk.Spawn("Spark", centre, Quaternion.identity, Vector3.one * 0.06f, spark,
                        direction * Range(4f, 8f), Spin(900f), Range(0.4f, 0.8f));
                }
            }
            // The stump has to be solid on the very next step.
            Physics.SyncTransforms();
        }

        static Vector3 RandomVector(System.Random random) =>
            new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f);

        /// <summary>The body's renderer bounds, in the sentry's local space.</summary>
        /// <summary>The body's solid extent in the sentry's own frame: its box, or its mesh collider's mesh.</summary>
        static Bounds LocalBounds(Transform body)
        {
            Transform root = body.parent;
            if (root != null && body.TryGetComponent(out BoxCollider box))
                return new Bounds(root.InverseTransformPoint(body.TransformPoint(box.center)), Vector3.Scale(box.size, body.lossyScale));
            if (root != null && body.TryGetComponent(out MeshCollider hull) && hull.sharedMesh != null)
            {
                Bounds mesh = hull.sharedMesh.bounds;
                return new Bounds(root.InverseTransformPoint(body.TransformPoint(mesh.center)), Vector3.Scale(mesh.size, body.lossyScale));
            }
            return new Bounds(new Vector3(0f, 0.65f, 0f), new Vector3(0.9f, 1.3f, 0.9f));
        }

        static void Flash(Vector3 position)
        {
            var light = new GameObject("Wreck Flash").AddComponent<Light>();
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.3f);
            light.range = 6f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<FadeLight>();
        }

        /// <summary>Fades a light out and removes it.</summary>
        class FadeLight : MonoBehaviour
        {
            const float Duration = 0.35f;
            float age;
            float start;
            Light light;

            void Awake()
            {
                light = GetComponent<Light>();
                start = light.intensity;
            }

            void Update()
            {
                age += Time.deltaTime;
                light.intensity = start * (1f - age / Duration);
                if (age >= Duration)
                    Destroy(gameObject);
            }
        }
    }
}
