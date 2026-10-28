using System.Collections.Generic;
using System.IO;
using GravBoots.Level;
using GravBoots.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GravBoots.Editor
{
    /// <summary>
    /// Tube elevators (<see cref="Elevator"/>) for the test bed and the Level Kit: the Tripo
    /// elevator model (scaled ×4.5; a greybox tube if the model is missing), glass and a ring of
    /// solid walls inside its frame so the doorway is the only way in or out, and a disc platform
    /// on an <see cref="MoverMode.OnStep"/> mover that fills the gap in the model's floor. A start
    /// elevator rises from a dark shaft below the floor; an exit elevator carries the
    /// <see cref="LevelExit"/> on its platform up and out of the top.
    /// </summary>
    public static class ElevatorBuilder
    {
        const string Folder = TestBedBuilder.Root + "/Art/Level";
        const string ModelPath = Folder + "/Elevator/Model/futuristic portal 3d model/futuristic_portal_3d_model.fbx";
        const string RingMeshPath = Folder + "/ElevatorRing.asset";
        const string GlassPath = TestBedBuilder.Root + "/Materials/Elevator_Glass.mat";

        const float WallThickness = 0.1f;
        const int WallSegments = 24;
        const int IgnoreRaycastLayer = 2;
        /// <summary>Start: how far the ride rises to the elevator's floor. Exit: how far it rises from it.</summary>
        public const float DefaultTravel = 6f;
        const float RideSpeed = 2f;

        /// <summary>The sizes an elevator is built to: the model's (measured at ×4.5), or the greybox's.</summary>
        struct Dims
        {
            public float Scale;
            /// <summary>Inside face of the glass and walls.</summary>
            public float WallRadius;
            public float DoorwayDegrees;
            public float PlatformRadius;
            /// <summary>Where the platform stops: the top of the model's floor ring (0 for the greybox).</summary>
            public float FloorHeight;
            /// <summary>Top of the glass and walls above the elevator's base.</summary>
            public float TubeTop;
        }

        static readonly Dims Model = new Dims
        {
            // At ×4.5: the floor ring is flat at 0.48 m from 0.85 m (the gap) out to 1.3 m, the
            // frame's posts stand 1.2-1.32 m out with the doorway posts at ±30°, and the crown
            // begins about 3.6 m up. Steps lead down from the doorway to the ground.
            Scale = 4.5f, WallRadius = 1.15f, DoorwayDegrees = 58f, PlatformRadius = 0.83f, FloorHeight = 0.48f, TubeTop = 3.6f,
        };

        static readonly Dims Greybox = new Dims
        {
            Scale = 1f, WallRadius = 1.5f, DoorwayDegrees = 64f, PlatformRadius = 1.42f, FloorHeight = 0f, TubeTop = 3f,
        };

        static bool HasModel => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;
        static Dims Current => HasModel ? Model : Greybox;

        /// <summary>The square opening a start elevator's shaft needs in the floor round it.</summary>
        public static float ShaftOpening => 2f * Current.PlatformRadius + 0.1f;

        /// <summary>
        /// An elevator standing on a floor at <paramref name="localPosition"/>, its doorway facing
        /// <paramref name="forward"/>. Start: the platform rises <paramref name="travel"/> from
        /// below to the elevator's floor. Exit: it rises <paramref name="travel"/> from there.
        /// </summary>
        internal static Elevator Build(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up,
            ElevatorRole role, TestBedBuilder.Palette palette, float travel = DefaultTravel, int spawnOrder = 0)
        {
            bool model = HasModel;
            Dims dims = Current;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.LookRotation(forward, up);
            var elevator = root.gameObject.AddComponent<Elevator>();
            elevator.role = role;

            bool start = role == ElevatorRole.Start;
            float floor = dims.FloorHeight;
            float bottom = start ? floor - travel : floor;
            float top = start ? floor : floor + travel;
            // The greybox tube runs down the shaft and, for an exit, up the whole ride; the model's
            // glass fills its frame, and the shaft below has walls of its own.
            float glassBottom = model ? floor : bottom;
            float tubeTop = model || start ? dims.TubeTop : travel + dims.TubeTop;

            Transform tube = Child(root, "Tube", Vector3.zero);
            if (model)
                Frame(tube, dims);
            var glass = Child(tube, "Glass", new Vector3(0f, glassBottom, 0f));
            glass.localScale = new Vector3(dims.WallRadius, tubeTop - glassBottom, dims.WallRadius);
            glass.gameObject.AddComponent<MeshFilter>().sharedMesh = GlassMesh(dims.DoorwayDegrees);
            var glassRenderer = glass.gameObject.AddComponent<MeshRenderer>();
            glassRenderer.sharedMaterial = Glass();
            glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Walls(tube, dims, glassBottom, model && !start ? floor + travel + 2f : tubeTop);
            if (!model)
            {
                Ring(tube, "Base Ring", 0f, palette.Dark, dims.WallRadius + 0.05f);
                Ring(tube, "Top Ring", tubeTop, palette.Dark, dims.WallRadius + 0.05f);
            }
            float shaftHalf = model ? dims.PlatformRadius + 0.05f : dims.WallRadius + 0.125f;
            if (start)
                Shaft(root, "Shaft", bottom - 0.5f, 0f, shaftHalf, true, palette);
            else if (model)
                // Up from the crown to above the top of the ride: the way out of the chamber.
                Shaft(root, "Exit Shaft", dims.Scale, top + 2.4f, shaftHalf, false, palette);

            var mover = root.gameObject.AddComponent<KinematicMover>();
            var platform = Child(root, "Platform", Vector3.zero);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(platform, false);
            disc.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            disc.transform.localScale = new Vector3(dims.PlatformRadius * 2f, 0.1f, dims.PlatformRadius * 2f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = palette.Blue;
            // The disc itself is the deck: round, filling the gap in the floor.
            disc.AddComponent<MeshCollider>().convex = true;
            Ring(platform, "Platform Rim", -0.05f, palette.Dark, dims.PlatformRadius);

            mover.platform = platform;
            mover.points = new[] { Child(root, "Bottom", new Vector3(0f, bottom, 0f)), Child(root, "Top", new Vector3(0f, top, 0f)) };
            mover.mode = MoverMode.OnStep;
            mover.speed = RideSpeed;
            mover.wait = 0f;
            mover.CachePath();
            mover.Evaluate(0f);
            elevator.mover = mover;

            if (start)
            {
                elevator.boardingSpawn = Spawn(root, "Start (elevator)", new Vector3(0f, bottom + 0.02f, 0f), spawnOrder);
                elevator.arrivalSpawn = Spawn(root, "Start (arrived)", new Vector3(0f, top + 0.02f, 0f), spawnOrder);
            }
            else
            {
                var exit = new GameObject("Exit", typeof(BoxCollider)).AddComponent<LevelExit>();
                exit.transform.SetParent(platform, false);
                var trigger = exit.GetComponent<BoxCollider>();
                trigger.isTrigger = true;
                // Well inside the platform, so the player is standing on it when the run ends.
                float side = dims.PlatformRadius;
                trigger.size = new Vector3(side, 2f, side);
                trigger.center = new Vector3(0f, 1f, 0f);
            }
            return elevator;
        }

        /// <summary>
        /// The model, solid in its own shape (collar, steps, posts). On Ignore Raycast with the
        /// walls, so the camera looks through the frame rather than pulling in behind it.
        /// </summary>
        static void Frame(Transform tube, Dims dims)
        {
            var frame = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), tube);
            frame.name = "Frame";
            frame.transform.localScale = Vector3.one * dims.Scale;
            frame.AddComponent<MeshCollider>();
            frame.layer = IgnoreRaycastLayer;
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static SpawnPoint Spawn(Transform root, string label, Vector3 localPosition, int order)
        {
            var spawn = new GameObject(label).AddComponent<SpawnPoint>();
            spawn.transform.SetParent(root, false);
            spawn.transform.localPosition = localPosition;
            spawn.label = label;
            spawn.order = order;
            return spawn;
        }

        /// <summary>
        /// Thin boxes round the tube, leaving the doorway (centred on +Z) open. On Unity's Ignore
        /// Raycast layer: the player still collides with them, but the camera rig ignores them, so
        /// it stays outside looking in through the glass instead of pulling in behind the player.
        /// </summary>
        static void Walls(Transform tube, Dims dims, float bottom, float top)
        {
            var walls = Child(tube, "Walls", Vector3.zero);
            walls.gameObject.layer = IgnoreRaycastLayer;
            float step = 360f / WallSegments;
            float chord = 2f * (dims.WallRadius + WallThickness) * Mathf.Tan(Mathf.Deg2Rad * step * 0.5f);
            for (int i = 0; i < WallSegments; i++)
            {
                float angle = (i + 0.5f) * step;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < dims.DoorwayDegrees * 0.5f)
                    continue;
                Quaternion turn = Quaternion.Euler(0f, angle, 0f);
                var wall = Child(walls, "Wall", turn * Vector3.forward * (dims.WallRadius + WallThickness * 0.5f) + Vector3.up * (bottom + top) * 0.5f);
                wall.localRotation = turn;
                wall.gameObject.layer = IgnoreRaycastLayer;
                var box = wall.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(chord, top - bottom, WallThickness);
            }
        }

        /// <summary>
        /// A dark square shaft from <paramref name="from"/> to <paramref name="to"/>: under a
        /// start elevator (closed at the bottom) or above an exit elevator (closed at the top),
        /// with a glowing band round it every 2 m so a ride reads as moving.
        /// </summary>
        static void Shaft(Transform root, string name, float from, float to, float halfWidth, bool closedBelow,
            TestBedBuilder.Palette palette)
        {
            var shaft = Child(root, name, Vector3.zero);
            float inner = halfWidth * 2f, t = 0.3f, height = to - from, y = (from + to) * 0.5f;
            TestBedBuilder.Box(shaft, "Wall +X", new Vector3((inner + t) * 0.5f, y, 0f), new Vector3(t, height, inner + 2f * t), palette.Dark);
            TestBedBuilder.Box(shaft, "Wall -X", new Vector3(-(inner + t) * 0.5f, y, 0f), new Vector3(t, height, inner + 2f * t), palette.Dark);
            TestBedBuilder.Box(shaft, "Wall +Z", new Vector3(0f, y, (inner + t) * 0.5f), new Vector3(inner, height, t), palette.Dark);
            TestBedBuilder.Box(shaft, "Wall -Z", new Vector3(0f, y, -(inner + t) * 0.5f), new Vector3(inner, height, t), palette.Dark);
            float capY = closedBelow ? from - t * 0.5f : to + t * 0.5f;
            TestBedBuilder.Box(shaft, closedBelow ? "Floor" : "Cap", new Vector3(0f, capY, 0f), new Vector3(inner + 2f * t, t, inner + 2f * t), palette.Dark);

            // Light bands just proud of the walls' inner faces (no colliders).
            for (float band = from + 1f; band < to - 0.5f; band += 2f)
            {
                for (int side = 0; side < 4; side++)
                {
                    var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    strip.name = "Light Band";
                    Object.DestroyImmediate(strip.GetComponent<Collider>());
                    strip.transform.SetParent(shaft, false);
                    Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                    strip.transform.localPosition = turn * new Vector3(0f, 0f, halfWidth - 0.01f) + Vector3.up * band;
                    strip.transform.localRotation = turn;
                    strip.transform.localScale = new Vector3(inner * 0.8f, 0.06f, 0.03f);
                    strip.GetComponent<MeshRenderer>().sharedMaterial = palette.BootsOn;
                    strip.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>A dark ring of <paramref name="radius"/> (its band about 6% as thick).</summary>
        static void Ring(Transform parent, string name, float height, Material material, float radius)
        {
            var ring = Child(parent, name, Vector3.up * height);
            ring.localScale = Vector3.one * radius;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            var renderer = ring.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        /// <summary>
        /// A unit open cylinder (radius 1, height 0–1, scaled per elevator) with the doorway,
        /// centred on +Z, cut out. One asset per doorway width.
        /// </summary>
        static Mesh GlassMesh(float doorwayDegrees)
        {
            string path = $"{Folder}/ElevatorGlass_{Mathf.RoundToInt(doorwayDegrees)}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            const int segments = 48;
            float start = doorwayDegrees * 0.5f, sweep = 360f - doorwayDegrees;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Deg2Rad * (start + sweep * i / segments);
                // Angles from +Z, turning toward +X.
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int k = 0; k < 2; k++)
                {
                    vertices.Add(outward + Vector3.up * k);
                    normals.Add(outward);
                    uvs.Add(new Vector2((float)i / segments, k));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 2;
                triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            mesh = new Mesh { name = "ElevatorGlass" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Folder);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>A unit-radius torus for the rings (scaled per ring).</summary>
        static Mesh RingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
            if (mesh != null)
                return mesh;
            mesh = GlowMeshes.Torus(1f, 0.06f, 64, 8);
            mesh.name = "ElevatorRing";
            Directory.CreateDirectory(Folder);
            AssetDatabase.CreateAsset(mesh, RingMeshPath);
            return mesh;
        }

        /// <summary>Faintly tinted, double-sided glass (seen from inside while riding).</summary>
        static Material Glass()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.6f, 0.85f, 1f, 0.18f));
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, GlassPath);
            return material;
        }
    }
}
