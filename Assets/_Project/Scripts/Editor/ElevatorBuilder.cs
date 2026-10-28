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
    /// Greybox tube elevators (<see cref="Elevator"/>) for the test bed and the Level Kit: a glass
    /// tube with a doorway (solid walls, so the doorway is the only way in or out), a disc
    /// platform on an <see cref="MoverMode.OnStep"/> mover, and rings at the base and top.
    /// A start elevator rises from a dark shaft below the floor; an exit elevator carries the
    /// <see cref="LevelExit"/> on its platform up through a tall tube.
    /// </summary>
    public static class ElevatorBuilder
    {
        const string Folder = TestBedBuilder.Root + "/Art/Level";
        const string TubeMeshPath = Folder + "/ElevatorTube.asset";
        const string RingMeshPath = Folder + "/ElevatorRing.asset";
        const string GlassPath = TestBedBuilder.Root + "/Materials/Elevator_Glass.mat";

        public const float TubeRadius = 1.5f;
        /// <summary>The doorway's width as an angle; at this radius it's about 1.6 m across.</summary>
        const float DoorwayDegrees = 64f;
        const float WallThickness = 0.1f;
        const int WallSegments = 24;
        const int IgnoreRaycastLayer = 2;
        /// <summary>Just inside the tube, so stepping off through the doorway crosses no gap.</summary>
        const float PlatformRadius = 1.42f;
        /// <summary>The square opening a start elevator's shaft needs in the floor round it.</summary>
        public const float ShaftOpening = 2f * PlatformRadius + 0.06f;
        /// <summary>Start: how far below the floor the ride begins. Exit: how far it rises.</summary>
        public const float DefaultTravel = 6f;
        const float StartTubeHeight = 3f;
        const float RideSpeed = 2f;

        /// <summary>
        /// An elevator standing on a floor at <paramref name="localPosition"/>, its doorway facing
        /// <paramref name="forward"/>. Start: the platform rises <paramref name="travel"/> from
        /// below to the floor. Exit: it rises <paramref name="travel"/> from the floor.
        /// </summary>
        internal static Elevator Build(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up,
            ElevatorRole role, TestBedBuilder.Palette palette, float travel = DefaultTravel, int spawnOrder = 0)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.LookRotation(forward, up);
            var elevator = root.gameObject.AddComponent<Elevator>();
            elevator.role = role;

            bool start = role == ElevatorRole.Start;
            float bottom = start ? -travel : 0f;
            float top = start ? 0f : travel;
            float tubeTop = start ? StartTubeHeight : travel + StartTubeHeight;

            // The tube: glass to look at, a ring of thin walls to stand inside, the doorway open.
            Transform tube = Child(root, "Tube", Vector3.zero);
            var glass = Child(tube, "Glass", new Vector3(0f, bottom, 0f));
            glass.localScale = new Vector3(1f, tubeTop - bottom, 1f);
            glass.gameObject.AddComponent<MeshFilter>().sharedMesh = TubeMesh();
            var glassRenderer = glass.gameObject.AddComponent<MeshRenderer>();
            glassRenderer.sharedMaterial = Glass();
            glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Walls(tube, bottom, tubeTop);
            Ring(tube, "Base Ring", 0f, palette.Dark);
            Ring(tube, "Top Ring", tubeTop, palette.Dark);
            if (start)
                Shaft(root, bottom, palette);

            var mover = root.gameObject.AddComponent<KinematicMover>();
            var platform = Child(root, "Platform", Vector3.zero);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(platform, false);
            disc.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            disc.transform.localScale = new Vector3(PlatformRadius * 2f, 0.1f, PlatformRadius * 2f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = palette.Blue;
            // The disc itself is the deck: round, right out to the tube.
            disc.AddComponent<MeshCollider>().convex = true;
            Ring(platform, "Platform Rim", -0.05f, palette.Dark, PlatformRadius);

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
                trigger.size = new Vector3(1.4f, 2f, 1.4f);
                trigger.center = new Vector3(0f, 1f, 0f);
            }
            return elevator;
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
        static void Walls(Transform tube, float bottom, float top)
        {
            var walls = Child(tube, "Walls", Vector3.zero);
            walls.gameObject.layer = IgnoreRaycastLayer;
            float step = 360f / WallSegments;
            float chord = 2f * (TubeRadius + WallThickness) * Mathf.Tan(Mathf.Deg2Rad * step * 0.5f);
            for (int i = 0; i < WallSegments; i++)
            {
                float angle = (i + 0.5f) * step;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < DoorwayDegrees * 0.5f)
                    continue;
                Quaternion turn = Quaternion.Euler(0f, angle, 0f);
                var wall = Child(walls, "Wall", turn * Vector3.forward * (TubeRadius + WallThickness * 0.5f) + Vector3.up * (bottom + top) * 0.5f);
                wall.localRotation = turn;
                wall.gameObject.layer = IgnoreRaycastLayer;
                var box = wall.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(chord, top - bottom, WallThickness);
            }
        }

        /// <summary>The dark shaft under a start elevator: four walls and a floor.</summary>
        static void Shaft(Transform root, float bottom, TestBedBuilder.Palette palette)
        {
            var shaft = Child(root, "Shaft", Vector3.zero);
            float inner = TubeRadius * 2f + 0.25f, t = 0.3f, depth = -bottom + 0.5f;
            float y = bottom + depth * 0.5f - 0.5f;
            TestBedBuilder.Box(shaft, "Shaft +X", new Vector3((inner + t) * 0.5f, y, 0f), new Vector3(t, depth, inner + 2f * t), palette.Dark);
            TestBedBuilder.Box(shaft, "Shaft -X", new Vector3(-(inner + t) * 0.5f, y, 0f), new Vector3(t, depth, inner + 2f * t), palette.Dark);
            TestBedBuilder.Box(shaft, "Shaft +Z", new Vector3(0f, y, (inner + t) * 0.5f), new Vector3(inner, depth, t), palette.Dark);
            TestBedBuilder.Box(shaft, "Shaft -Z", new Vector3(0f, y, -(inner + t) * 0.5f), new Vector3(inner, depth, t), palette.Dark);
            TestBedBuilder.Box(shaft, "Shaft Floor", new Vector3(0f, bottom - 0.5f - t * 0.5f, 0f), new Vector3(inner + 2f * t, t, inner + 2f * t), palette.Dark);
        }

        /// <summary>A dark ring of <paramref name="radius"/> (its band about 6% as thick).</summary>
        static void Ring(Transform parent, string name, float height, Material material, float radius = TubeRadius + 0.05f)
        {
            var ring = Child(parent, name, Vector3.up * height);
            ring.localScale = Vector3.one * radius;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            var renderer = ring.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        /// <summary>A unit open cylinder (radius 1, height 0–1) with the doorway cut out, for the glass.</summary>
        static Mesh TubeMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TubeMeshPath);
            if (mesh != null)
                return mesh;
            const int segments = 48;
            float start = DoorwayDegrees * 0.5f, sweep = 360f - DoorwayDegrees;
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
                    vertices.Add(outward * TubeRadius + Vector3.up * k);
                    normals.Add(outward);
                    uvs.Add(new Vector2((float)i / segments, k));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 2;
                triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            mesh = new Mesh { name = "ElevatorTube" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Folder);
            AssetDatabase.CreateAsset(mesh, TubeMeshPath);
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
