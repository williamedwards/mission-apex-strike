using System.IO;
using GravBoots.Cameras;
using GravBoots.Debugging;
using GravBoots.Gravity;
using GravBoots.Level;
using GravBoots.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace GravBoots.Editor
{
    /// <summary>
    /// Generates the Phase 1A camera test bed (docs/PLAN.md §2, Phase 1A): greybox areas for
    /// testing the motor and camera in every orientation, the Player prefab, the camera rig
    /// and the debug tools. Safe to re-run; it overwrites the scene and the prefab.
    /// Headless: <c>unity run . -- -executeMethod GravBoots.Editor.TestBedBuilder.Build -quit</c>
    /// </summary>
    public static class TestBedBuilder
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/CameraTestBed.unity";
        const string SandboxPath = Root + "/Scenes/Sandbox.unity";
        const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";
        const string GridTexturePath = Root + "/Art/GreyboxGrid.png";
        const string MaterialFolder = Root + "/Materials";
        const string SettingsFolder = Root + "/Settings";
        const string InputPath = SettingsFolder + "/GravBootsControls.inputactions";

        const int PlayerLayer = 8;
        const float AreaSpacing = 70f;

        class Palette
        {
            public Material Floor, Wall, Ceiling, Dark, Orange, Blue, PlayerBody, Visor;
        }

        [MenuItem("GravBoots/Build Camera Test Bed")]
        public static void Build()
        {
            Texture2D grid = EnsureGridTexture();
            Palette palette = EnsureMaterials(grid);
            var motorSettings = EnsureAsset<MotorSettings>(SettingsFolder + "/MotorSettings.asset");
            var cameraSettings = EnsureAsset<CameraSettings>(SettingsFolder + "/CameraSettings.asset");
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputAsset == null)
                throw new FileNotFoundException("Input actions asset not found", InputPath);

            GameObject playerPrefab = BuildPlayerPrefab(motorSettings, inputAsset, palette);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Opening a new scene unloads assets nothing references yet, which leaves earlier
            // handles dead (they then serialize as null), so load the camera settings again.
            cameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(SettingsFolder + "/CameraSettings.asset");
            SetupLighting();

            var areas = new GameObject("Areas").transform;
            BuildCubeRoom(Area(areas, "Cube Room", 0), palette);
            BuildMetricsGym(Area(areas, "Metrics Gym", 1), palette);
            BuildCorridors(Area(areas, "Corridors", 2), palette);
            BuildCrawlspace(Area(areas, "Crawlspace", 3), palette);
            BuildOutsideCorner(Area(areas, "Outside Corner", 4), palette);
            BuildPitRoom(Area(areas, "Pit Under Ceiling", 5), palette);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -2f), Quaternion.identity);
            BuildCameraAndDebug(player, cameraSettings);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SandboxPath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"[GravBoots] Built {ScenePath}");
        }

        // ---------------------------------------------------------------- assets

        static Texture2D EnsureGridTexture()
        {
            if (!File.Exists(GridTexturePath))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int ex = Mathf.Min(x, size - 1 - x), ey = Mathf.Min(y, size - 1 - y);
                    int mx = Mathf.Abs(x - size / 2), my = Mathf.Abs(y - size / 2);
                    byte value = 235;
                    if (mx < 1 || my < 1) value = 210;         // faint half-metre line
                    if (ex < 3 || ey < 3) value = 140;         // metre line
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
                texture.SetPixels32(pixels);
                Directory.CreateDirectory(Path.GetDirectoryName(GridTexturePath));
                File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(GridTexturePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(GridTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        static Palette EnsureMaterials(Texture2D grid)
        {
            return new Palette
            {
                Floor = EnsureMaterial("Greybox_Floor", new Color(0.62f, 0.64f, 0.68f), grid),
                Wall = EnsureMaterial("Greybox_Wall", new Color(0.82f, 0.84f, 0.87f), grid),
                Ceiling = EnsureMaterial("Greybox_Ceiling", new Color(0.50f, 0.52f, 0.58f), grid),
                Dark = EnsureMaterial("Greybox_Dark", new Color(0.22f, 0.24f, 0.27f), grid),
                Orange = EnsureMaterial("Accent_Orange", new Color(1f, 0.55f, 0.15f), grid),
                Blue = EnsureMaterial("Accent_Blue", new Color(0.2f, 0.55f, 1f), grid),
                PlayerBody = EnsureMaterial("Player_Body", new Color(0.88f, 0.88f, 0.92f), null),
                Visor = EnsureMaterial("Player_Visor", new Color(1f, 0.55f, 0.15f), null, emissive: true),
            };
        }

        static Material EnsureMaterial(string name, Color color, Texture texture, bool emissive = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.25f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject BuildPlayerPrefab(MotorSettings motorSettings, InputActionAsset inputAsset, Palette palette)
        {
            var root = new GameObject("Player");
            root.layer = PlayerLayer;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<Rigidbody>();
            root.AddComponent<GravityBody>();
            var motor = root.AddComponent<PlayerMotor>();
            var reader = root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerController>();

            Set(motor, "settings", motorSettings);
            SetInt(motor, "collisionMask", ~(1 << PlayerLayer));
            Set(reader, "actions", inputAsset);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Primitive(visual, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), palette.PlayerBody);
            Primitive(visual, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.5f, 0.3f), new Vector3(0.45f, 0.14f, 0.18f), palette.Visor);
            Primitive(visual, PrimitiveType.Cube, "Boot_L", new Vector3(-0.15f, 0.08f, 0.06f), new Vector3(0.2f, 0.16f, 0.36f), palette.Dark);
            Primitive(visual, PrimitiveType.Cube, "Boot_R", new Vector3(0.15f, 0.08f, 0.06f), new Vector3(0.2f, 0.16f, 0.36f), palette.Dark);
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                t.gameObject.layer = PlayerLayer;

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- scene setup

        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
            RenderSettings.skybox = null;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildCameraAndDebug(GameObject player, CameraSettings cameraSettings)
        {
            var motor = player.GetComponent<PlayerMotor>();
            var reader = player.GetComponent<PlayerInputReader>();
            var controller = player.GetComponent<PlayerController>();

            var rigObject = new GameObject("CameraRig");
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(rigObject.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.WorldUpOverride = rigObject.transform;

            var vcamObject = new GameObject("PlayerCam");
            var vcam = vcamObject.AddComponent<CinemachineCamera>();
            vcam.Follow = pivot;
            vcam.Lens.FieldOfView = cameraSettings.fieldOfView;
            vcam.Lens.NearClipPlane = 0.05f;
            var follow = vcamObject.AddComponent<CinemachineThirdPersonFollow>();
            // CameraRig handles obstacles itself (smoothly); see CameraRig.AvoidObstacles.
            follow.AvoidObstacles.Enabled = false;

            var rig = rigObject.AddComponent<CameraRig>();
            Set(rig, "target", player.transform);
            Set(rig, "pivot", pivot);
            Set(rig, "input", reader);
            Set(rig, "settings", cameraSettings);
            Set(rig, "virtualCamera", vcam);
            SetInt(rig, "obstacleMask", ~((1 << PlayerLayer) | (1 << 2)));
            Set(controller, "cameraRig", rig);

            var debugObject = new GameObject("Debug");
            var keys = debugObject.AddComponent<DebugGravityKeys>();
            var overlay = debugObject.AddComponent<DebugOverlay>();
            var popProbe = debugObject.AddComponent<CameraPopProbe>();
            Set(popProbe, "cameraRig", rig);
            SetInt(popProbe, "solidMask", ~((1 << PlayerLayer) | (1 << 2)));
            Set(overlay, "popProbe", popProbe);
            Set(keys, "input", reader);
            Set(keys, "motor", motor);
            Set(keys, "cameraRig", rig);
            Set(keys, "overlay", overlay);
            Set(overlay, "motor", motor);
            Set(overlay, "controller", controller);
            Set(overlay, "cameraRig", rig);
            Set(overlay, "keys", keys);
        }

        // ---------------------------------------------------------------- areas

        static Transform Area(Transform parent, string name, int index)
        {
            var area = new GameObject(name).transform;
            area.SetParent(parent, false);
            area.localPosition = new Vector3(index * AreaSpacing, 0f, 0f);
            return area;
        }

        static void Spawn(Transform area, string label, int order, Vector3 localPosition, float yaw = 0f)
        {
            var spawn = new GameObject("Spawn").AddComponent<SpawnPoint>();
            spawn.transform.SetParent(area, false);
            spawn.transform.localPosition = localPosition;
            spawn.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            spawn.label = label;
            spawn.order = order;
        }

        static void PointLight(Transform area, Vector3 localPosition, float range, float intensity = 2.5f)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(area, false);
            light.transform.localPosition = localPosition;
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>Six slabs enclosing an inner volume whose floor top is at <paramref name="floorCentre"/>.</summary>
        static void Enclosure(Transform parent, Vector3 floorCentre, Vector3 inner, Palette palette, float t = 1f)
        {
            Vector3 c = floorCentre + Vector3.up * (inner.y * 0.5f);
            Box(parent, "Floor", floorCentre - Vector3.up * (t * 0.5f), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t), palette.Floor);
            Box(parent, "Ceiling", c + Vector3.up * (inner.y * 0.5f + t * 0.5f), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t), palette.Ceiling);
            Box(parent, "Wall +X", c + Vector3.right * (inner.x * 0.5f + t * 0.5f), new Vector3(t, inner.y, inner.z), palette.Wall);
            Box(parent, "Wall -X", c - Vector3.right * (inner.x * 0.5f + t * 0.5f), new Vector3(t, inner.y, inner.z), palette.Wall);
            Box(parent, "Wall +Z", c + Vector3.forward * (inner.z * 0.5f + t * 0.5f), new Vector3(inner.x, inner.y, t), palette.Wall);
            Box(parent, "Wall -Z", c - Vector3.forward * (inner.z * 0.5f + t * 0.5f), new Vector3(inner.x, inner.y, t), palette.Wall);
        }

        static void BuildCubeRoom(Transform area, Palette p)
        {
            const float s = 16f;
            Enclosure(area, Vector3.zero, Vector3.one * s, p);
            // One distinct landmark per face so you can always tell which way is which.
            Box(area, "Landmark Floor (orange pillar)", new Vector3(0f, 1.5f, 3f), new Vector3(1f, 3f, 1f), p.Orange);
            Box(area, "Landmark Ceiling (blue block)", new Vector3(0f, s - 1f, 0f), new Vector3(3f, 2f, 3f), p.Blue);
            Box(area, "Landmark +X (orange stripe)", new Vector3(s * 0.5f - 0.1f, s * 0.5f, 0f), new Vector3(0.2f, 1f, s), p.Orange);
            Box(area, "Landmark -X (blue stripe)", new Vector3(-s * 0.5f + 0.1f, s * 0.5f, 0f), new Vector3(0.2f, s, 1f), p.Blue);
            Box(area, "Landmark +Z (dark block)", new Vector3(3f, 6f, s * 0.5f - 1f), new Vector3(4f, 4f, 2f), p.Dark);
            Box(area, "Landmark -Z (dark frame)", new Vector3(0f, 2f, -s * 0.5f + 0.25f), new Vector3(3f, 4f, 0.5f), p.Dark);
            // Small obstacles to walk around and step over on the floor.
            Box(area, "Step 0.2", new Vector3(-4f, 0.1f, -2f), new Vector3(2f, 0.2f, 2f), p.Dark);
            Box(area, "Step 0.35", new Vector3(-4f, 0.175f, 1f), new Vector3(2f, 0.35f, 2f), p.Dark);
            Box(area, "Crate", new Vector3(4f, 0.75f, -3f), new Vector3(1.5f, 1.5f, 1.5f), p.Dark);
            PointLight(area, new Vector3(0f, s * 0.5f, 0f), 30f, 3f);
            Spawn(area, "Cube Room: all six faces are walkable (keys 1–6)", 0, new Vector3(0f, 0.05f, -2f));
        }

        static void BuildMetricsGym(Transform area, Palette p)
        {
            Box(area, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 50f), p.Floor);

            // Ramps: 20/35/50 are walkable (limit 50°), 60/70 are not.
            float[] angles = { 20f, 35f, 50f, 60f, 70f };
            const float length = 6f, width = 3f;
            for (int i = 0; i < angles.Length; i++)
            {
                float x = -20f + i * 5f;
                Ramp(area, $"Ramp {angles[i]:0}°", new Vector3(x, 0f, 2f), angles[i], length, width, angles[i] <= 50f ? p.Wall : p.Dark, p);
            }

            // Steps: 0.2 and 0.35 can be stepped up (limit 0.35 m), 0.5 needs a jump.
            float[] steps = { 0.2f, 0.35f, 0.5f };
            for (int i = 0; i < steps.Length; i++)
                Box(area, $"Step {steps[i]}", new Vector3(8f + i * 4f, steps[i] * 0.5f, 2f), new Vector3(3f, steps[i], 3f), i < 2 ? p.Wall : p.Dark);

            // Jump gaps between 1 m platforms.
            float[] gaps = { 2f, 3f, 4f, 5f };
            for (int i = 0; i < gaps.Length; i++)
            {
                float z = -6f - i * 6f;
                Box(area, $"Gap {gaps[i]} A", new Vector3(-12f, 0.5f, z), new Vector3(4f, 1f, 3f), p.Wall);
                Box(area, $"Gap {gaps[i]} B", new Vector3(-12f + 4f + gaps[i], 0.5f, z), new Vector3(4f, 1f, 3f), p.Orange);
            }

            // Drop heights: ledges 1, 2, 4 and 8 m with a ramp-free top reachable by debug gravity.
            float[] drops = { 1f, 2f, 4f, 8f };
            for (int i = 0; i < drops.Length; i++)
                Box(area, $"Ledge {drops[i]} m", new Vector3(8f + i * 5f, drops[i] * 0.5f, -12f), new Vector3(3f, drops[i], 3f), p.Blue);

            // Corridor width gauges, 3 m tall.
            float[] widths = { 2f, 3f, 4f };
            for (int i = 0; i < widths.Length; i++)
            {
                float x = 8f + i * 8f, half = widths[i] * 0.5f + 0.25f;
                Box(area, $"Gauge {widths[i]} m L", new Vector3(x - half, 1.5f, 14f), new Vector3(0.5f, 3f, 8f), p.Dark);
                Box(area, $"Gauge {widths[i]} m R", new Vector3(x + half, 1.5f, 14f), new Vector3(0.5f, 3f, 8f), p.Dark);
                Box(area, $"Gauge {widths[i]} m Roof", new Vector3(x, 3.25f, 14f), new Vector3(widths[i] + 1f, 0.5f, 8f), p.Ceiling);
            }

            Spawn(area, "Metrics Gym: ramps, steps, gaps, drops, corridor widths", 1, new Vector3(-10f, 0.05f, -2f));
        }

        static void Ramp(Transform area, string name, Vector3 foot, float angle, float length, float width, Material material, Palette p)
        {
            const float thickness = 0.5f;
            Quaternion rotation = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 centre = foot + rotation * new Vector3(0f, -thickness * 0.5f, length * 0.5f);
            Box(area, name, centre, new Vector3(width, thickness, length), material, rotation);

            // Landing at the top for the walkable ones, so walking up has somewhere to go.
            Vector3 top = foot + rotation * new Vector3(0f, 0f, length);
            if (angle <= 50f)
                Box(area, name + " Landing", new Vector3(top.x, top.y * 0.5f, top.z + 1.5f), new Vector3(width, top.y, 3f), p.Wall);
        }

        static void BuildCorridors(Transform area, Palette p)
        {
            const float w = 3f, h = 3f, t = 0.5f;
            // Straight corridor along +Z, then a left turn along -X: walk on both walls and the roof.
            Box(area, "Floor A", new Vector3(0f, -t * 0.5f, 15f), new Vector3(w + 2 * t, t, 30f + 2 * t), p.Floor);
            Box(area, "Roof A", new Vector3(0f, h + t * 0.5f, 15f), new Vector3(w + 2 * t, t, 30f + 2 * t), p.Ceiling);
            Box(area, "Wall A +X", new Vector3(w * 0.5f + t * 0.5f, h * 0.5f, 15f), new Vector3(t, h, 30f + 2 * t), p.Wall);
            Box(area, "Wall A -X", new Vector3(-w * 0.5f - t * 0.5f, h * 0.5f, 15f - w * 0.5f), new Vector3(t, h, 30f - w + 2 * t), p.Wall);
            Box(area, "End A -Z", new Vector3(0f, h * 0.5f, -t * 0.5f), new Vector3(w, h, t), p.Dark);
            Box(area, "End A +Z", new Vector3(0f, h * 0.5f, 30f + t * 0.5f), new Vector3(w, h, t), p.Dark);

            float z = 30f - w * 0.5f, len = 15f, cx = -w * 0.5f - len * 0.5f;
            Box(area, "Floor B", new Vector3(cx, -t * 0.5f, z), new Vector3(len, t, w + 2 * t), p.Floor);
            Box(area, "Roof B", new Vector3(cx, h + t * 0.5f, z), new Vector3(len, t, w + 2 * t), p.Ceiling);
            Box(area, "Wall B +Z", new Vector3(cx, h * 0.5f, z + w * 0.5f + t * 0.5f), new Vector3(len, h, t), p.Wall);
            Box(area, "Wall B -Z", new Vector3(cx, h * 0.5f, z - w * 0.5f - t * 0.5f), new Vector3(len, h, t), p.Wall);
            Box(area, "End B", new Vector3(-w * 0.5f - len - t * 0.5f, h * 0.5f, z), new Vector3(t, h, w), p.Dark);
            Box(area, "Stripe", new Vector3(w * 0.5f - 0.05f, 1.5f, 15f), new Vector3(0.1f, 0.3f, 30f), p.Orange);

            for (int i = 0; i < 4; i++)
                PointLight(area, new Vector3(0f, h - 0.5f, 4f + i * 8f), 10f, 1.5f);
            PointLight(area, new Vector3(cx, h - 0.5f, z), 12f, 1.5f);
            Spawn(area, "Corridors: 3 m × 3 m, straight then a left turn", 2, new Vector3(0f, 0.05f, 2f));
        }

        static void BuildCrawlspace(Transform area, Palette p)
        {
            // 2.2 m clear height against a 1.8 m capsule: the stress case for turning over.
            Enclosure(area, Vector3.zero, new Vector3(12f, 2.2f, 12f), p, 0.5f);
            Box(area, "Pillar", new Vector3(2f, 1.1f, 2f), new Vector3(1f, 2.2f, 1f), p.Orange);
            PointLight(area, new Vector3(0f, 1.8f, 0f), 14f, 2f);
            Spawn(area, "Crawlspace: 2.2 m ceiling, try 2 (ceiling) and back", 3, new Vector3(-3f, 0.05f, -3f), 45f);
        }

        static void BuildOutsideCorner(Transform area, Palette p)
        {
            Box(area, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), p.Floor);
            Box(area, "Block 8 m", new Vector3(0f, 4f, 6f), new Vector3(8f, 8f, 8f), p.Wall);
            Box(area, "Block Stripe", new Vector3(0f, 8.05f, 6f), new Vector3(1f, 0.1f, 8f), p.Orange);
            Box(area, "Floating Slab", new Vector3(-12f, 6f, 6f), new Vector3(6f, 1f, 6f), p.Blue);
            Box(area, "Thin Wall", new Vector3(12f, 3f, 6f), new Vector3(0.5f, 6f, 8f), p.Dark);
            Spawn(area, "Outside Corner: walk over block edges (G on a face)", 4, new Vector3(0f, 0.05f, -4f));
        }

        static void BuildPitRoom(Transform area, Palette p)
        {
            const float w = 20f, h = 10f, hole = 6f, depth = 12f, t = 1f;
            float side = (w - hole) * 0.5f;
            float offset = hole * 0.5f + side * 0.5f;
            // Floor with a square hole in the middle.
            Box(area, "Floor N", new Vector3(0f, -t * 0.5f, offset), new Vector3(w, t, side), p.Floor);
            Box(area, "Floor S", new Vector3(0f, -t * 0.5f, -offset), new Vector3(w, t, side), p.Floor);
            Box(area, "Floor E", new Vector3(offset, -t * 0.5f, 0f), new Vector3(side, t, hole), p.Floor);
            Box(area, "Floor W", new Vector3(-offset, -t * 0.5f, 0f), new Vector3(side, t, hole), p.Floor);
            Box(area, "Ceiling", new Vector3(0f, h + t * 0.5f, 0f), new Vector3(w + 2 * t, t, w + 2 * t), p.Ceiling);
            Box(area, "Wall +X", new Vector3(w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, w), p.Wall);
            Box(area, "Wall -X", new Vector3(-w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, w), p.Wall);
            Box(area, "Wall +Z", new Vector3(0f, h * 0.5f, w * 0.5f + t * 0.5f), new Vector3(w + 2 * t, h, t), p.Wall);
            Box(area, "Wall -Z", new Vector3(0f, h * 0.5f, -w * 0.5f - t * 0.5f), new Vector3(w + 2 * t, h, t), p.Wall);
            Box(area, "Ceiling Target", new Vector3(0f, h - 0.05f, 0f), new Vector3(hole, 0.1f, hole), p.Orange);

            // The shaft below; it has a floor so a fall is survivable until kill volumes exist.
            float sy = -t - depth * 0.5f;
            Box(area, "Shaft +X", new Vector3(hole * 0.5f + 0.25f, sy, 0f), new Vector3(0.5f, depth, hole), p.Dark);
            Box(area, "Shaft -X", new Vector3(-hole * 0.5f - 0.25f, sy, 0f), new Vector3(0.5f, depth, hole), p.Dark);
            Box(area, "Shaft +Z", new Vector3(0f, sy, hole * 0.5f + 0.25f), new Vector3(hole + 1f, depth, 0.5f), p.Dark);
            Box(area, "Shaft -Z", new Vector3(0f, sy, -hole * 0.5f - 0.25f), new Vector3(hole + 1f, depth, 0.5f), p.Dark);
            Box(area, "Shaft Bottom", new Vector3(0f, -t - depth - 0.25f, 0f), new Vector3(hole + 1f, 0.5f, hole + 1f), p.Orange);

            PointLight(area, new Vector3(0f, h * 0.6f, 0f), 25f, 3f);
            PointLight(area, new Vector3(0f, -t - depth * 0.5f, 0f), 10f, 1.5f);
            Spawn(area, "Pit Under Ceiling: press 2, walk over the pit, look down", 5, new Vector3(0f, 0.05f, -7f));
        }

        // ---------------------------------------------------------------- helpers

        static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(GreyboxBox));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var box = go.GetComponent<GreyboxBox>();
            box.size = size;
            box.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
