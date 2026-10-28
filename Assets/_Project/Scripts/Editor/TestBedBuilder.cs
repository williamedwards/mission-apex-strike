using System;
using System.IO;
using GravBoots.Cameras;
using GravBoots.Debugging;
using GravBoots.Gravity;
using GravBoots.Hazards;
using GravBoots.Level;
using GravBoots.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

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
        internal const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/CameraTestBed.unity";
        const string SandboxPath = Root + "/Scenes/Sandbox.unity";
        internal const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";
        const string PlayerModelPath = Root + "/Art/Characters/Player/Model/tripo_convert_b0228436-c2f5-499d-a62d-b1bf14bdc437.fbx";
        const string PlayerAnimatorPath = Root + "/Art/Characters/Player/PlayerAnimator.controller";
        const string EffectsFolder = Root + "/Art/Characters/Player/Effects";
        const string AnimationsFolder = Root + "/Art/Characters/Player/Animations";
        /// <summary>A clip anywhere under <see cref="AnimationsFolder"/> with this name drives the magnetized walk.</summary>
        const string MagnetizedWalkClip = "DeliberateWalk";
        const string GridTexturePath = Root + "/Art/GreyboxGrid.png";
        const string PanelTexturePath = Root + "/Art/MagneticPanel.png";
        const string MaterialFolder = Root + "/Materials";
        internal const string SettingsFolder = Root + "/Settings";
        const string InputPath = SettingsFolder + "/GravBootsControls.inputactions";

        const int PlayerLayer = Layers.Player;
        const float AreaSpacing = 70f;
        /// <summary>Standard quarter-pipe radius (docs/LEVEL_METRICS.md), chosen in playtesting.</summary>
        internal const float QuarterPipeRadius = 2.5f;

        internal class Palette
        {
            public Material Floor, Wall, Ceiling, Dark, Orange, Blue, PlayerBody, Visor;
            public Material Magnetic, BootsOff, BootsOn, Marker, Guide;
            public Material CheckpointIdle, CheckpointLit, Exit, TurretEye, SentryShell;
        }

        [MenuItem("GravBoots/Build Camera Test Bed")]
        public static void Build()
        {
            EnsureTag(Layers.LethalTag);
            Palette palette = EnsureMaterials();
            var motorSettings = EnsureAsset<MotorSettings>(SettingsFolder + "/MotorSettings.asset");
            var bootsSettings = EnsureAsset<BootsSettings>(SettingsFolder + "/BootsSettings.asset");
            var cameraSettings = EnsureAsset<CameraSettings>(SettingsFolder + "/CameraSettings.asset");
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputAsset == null)
                throw new FileNotFoundException("Input actions asset not found", InputPath);

            GameObject playerPrefab = BuildPlayerPrefab(motorSettings, bootsSettings, inputAsset, palette);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Opening a new scene unloads assets nothing references yet, which leaves earlier
            // handles dead (they then serialize as null), so load the camera settings again.
            cameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(SettingsFolder + "/CameraSettings.asset");
            palette = EnsureMaterials();
            SetupLighting();

            var areas = new GameObject("Areas").transform;
            BuildCubeRoom(Area(areas, "Cube Room", 0), palette);
            BuildMetricsGym(Area(areas, "Metrics Gym", 1), palette);
            BuildCorridors(Area(areas, "Corridors", 2), palette);
            BuildCrawlspace(Area(areas, "Crawlspace", 3), palette);
            BuildOutsideCorner(Area(areas, "Outside Corner", 4), palette);
            BuildPitRoom(Area(areas, "Pit Under Ceiling", 5), palette);
            BuildBootsGym(Area(areas, "Boots Gym", 6), palette);
            // The Boots Gym is wide enough to fill slot 7 as well.
            SpawnPoint newest = BuildCheckpointRun(Area(areas, "Checkpoint Run", 8), palette);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -2f), Quaternion.identity);
            BuildCameraAndDebug(player, cameraSettings);
            BuildBootsFeedback(player, palette);
            // Start in the newest area; N still cycles through all of them.
            Set(BuildSession(player), "startSpawn", newest);

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

        static Texture2D EnsureGridTexture() => EnsureTexture(GridTexturePath, (x, y, size) =>
        {
            int ex = Mathf.Min(x, size - 1 - x), ey = Mathf.Min(y, size - 1 - y);
            int mx = Mathf.Abs(x - size / 2), my = Mathf.Abs(y - size / 2);
            byte value = 235;
            if (mx < 1 || my < 1) value = 210;         // faint half-metre line
            if (ex < 3 || ey < 3) value = 140;         // metre line
            return value;
        });

        /// <summary>Magnetic panels: a bright 1 m tile with a bevelled border, unlike the plain grid.</summary>
        static Texture2D EnsurePanelTexture() => EnsureTexture(PanelTexturePath, (x, y, size) =>
        {
            int e = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (e < 4) return 120;                     // seam between panels
            if (e < 22) return 205;                    // bevel
            if (e < 25) return 175;                    // inner line
            return 250;
        });

        static Texture2D EnsureTexture(string path, Func<int, int, int, byte> shade)
        {
            if (!File.Exists(path))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte value = shade(x, y, size);
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
                texture.SetPixels32(pixels);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Palette EnsureMaterials()
        {
            Texture2D grid = EnsureGridTexture();
            Texture2D panel = EnsurePanelTexture();
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
                Magnetic = EnsureMaterial("Magnetic_Panel", new Color(0.9f, 0.95f, 1f), panel, smoothness: 0.45f),
                BootsOff = EnsureMaterial("Boots_Off", new Color(0.22f, 0.24f, 0.27f), null),
                BootsOn = EnsureMaterial("Boots_On", new Color(0.35f, 0.8f, 1f), null, emissive: true),
                Marker = EnsureMaterial("Landing_Marker", Color.white, null, shader: "Universal Render Pipeline/Unlit"),
                // Sprites/Default honours LineRenderer vertex colours, which URP Unlit ignores.
                Guide = EnsureMaterial("Landing_Guide", Color.white, null, shader: "Sprites/Default"),
                CheckpointIdle = EnsureMaterial("Checkpoint_Idle", new Color(0.3f, 0.45f, 0.4f), null),
                CheckpointLit = EnsureMaterial("Checkpoint_Lit", new Color(0.3f, 1f, 0.6f), null, emissive: true),
                Exit = EnsureMaterial("Exit_Pad", new Color(1f, 0.85f, 0.2f), null, emissive: true),
                TurretEye = EnsureMaterial("Turret_Eye", new Color(1f, 0.15f, 0.1f), null, emissive: true),
                SentryShell = EnsureMaterial("Sentry_Shell", new Color(0.93f, 0.93f, 0.95f), null, smoothness: 0.6f),
            };
        }

        static Material EnsureMaterial(string name, Color color, Texture texture, bool emissive = false,
            float smoothness = 0.25f, string shader = "Universal Render Pipeline/Lit")
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureTag(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
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

        static GameObject BuildPlayerPrefab(MotorSettings motorSettings, BootsSettings bootsSettings,
            InputActionAsset inputAsset, Palette palette)
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
            root.AddComponent<Health>();
            root.AddComponent<PlayerSensor>();
            var boots = root.AddComponent<MagneticBoots>();
            Set(boots, "settings", bootsSettings);
            Set(boots, "input", reader);

            Set(motor, "settings", motorSettings);
            SetInt(motor, "collisionMask", ~(1 << PlayerLayer));
            Set(reader, "actions", inputAsset);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model != null)
            {
                var character = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                character.name = "Character";
                var animator = character.GetComponent<Animator>();
                animator.Rebind();
                animator.runtimeAnimatorController = BuildPlayerAnimator(PlayerModelPath, animator.humanScale,
                    out float magnetizedWalkSpeed, out float runClipSpeed);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<PlayerAnimation>().Bind(motor, animator, boots, magnetizedWalkSpeed, runClipSpeed);
                character.AddComponent<FootPlanting>().Bind(motor, boots, animator);
                BuildBootsGlow(root, boots, animator);
                foreach (Transform t in root.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = PlayerLayer;
                return SavePlayerPrefab(root);
            }
            // No character model: the original capsule placeholder, whose boot blocks change colour.
            Primitive(visual, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), palette.PlayerBody);
            Primitive(visual, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.5f, 0.3f), new Vector3(0.45f, 0.14f, 0.18f), palette.Visor);
            GameObject bootL = Primitive(visual, PrimitiveType.Cube, "Boot_L", new Vector3(-0.15f, 0.08f, 0.06f), new Vector3(0.2f, 0.16f, 0.36f), palette.BootsOff);
            GameObject bootR = Primitive(visual, PrimitiveType.Cube, "Boot_R", new Vector3(0.15f, 0.08f, 0.06f), new Vector3(0.2f, 0.16f, 0.36f), palette.BootsOff);
            var bootsVisual = root.AddComponent<BootsVisual>();
            Set(bootsVisual, "boots", boots);
            Set(bootsVisual, "offMaterial", palette.BootsOff);
            Set(bootsVisual, "onMaterial", palette.BootsOn);
            var renderers = new SerializedObject(bootsVisual);
            SerializedProperty list = renderers.FindProperty("bootRenderers");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = bootL.GetComponent<Renderer>();
            list.GetArrayElementAtIndex(1).objectReferenceValue = bootR.GetComponent<Renderer>();
            renderers.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                t.gameObject.layer = PlayerLayer;
            return SavePlayerPrefab(root);
        }

        static GameObject SavePlayerPrefab(GameObject root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- boots glow

        /// <summary>
        /// The magnetized effect on the character: per ankle, two counter-spinning energy rings in
        /// a soft halo, rising sparks, and a glow under the sole. <see cref="BootsGlow"/> shows it
        /// only while magnetized.
        /// </summary>
        static void BuildBootsGlow(GameObject root, MagneticBoots boots, Animator animator)
        {
            Directory.CreateDirectory(EffectsFolder);
            Mesh ring = SaveMesh(GlowMeshes.Torus(0.105f, 0.006f), "BootRing");
            Mesh innerRing = SaveMesh(GlowMeshes.Torus(0.09f, 0.004f), "BootRingInner");
            Mesh halo = SaveMesh(GlowMeshes.Halo(0.07f, 0.105f, 0.16f), "BootHalo");
            Mesh sole = SaveMesh(GlowMeshes.Disc(0.22f), "SoleGlow");
            // Rings blend normally so they read on light floors; halo, sole and sparks add light.
            Material core = GlowMaterial("Boots_RingCore", new Color(0.7f, 0.95f, 1f, 1f), additive: false, null);
            Material glow = GlowMaterial("Boots_Glow", new Color(0.2f, 0.6f, 1f, 0.8f), additive: true, null);
            Material spark = GlowMaterial("Boots_Spark", new Color(0.55f, 0.85f, 1f, 1f), additive: true, EnsureSoftDotTexture());

            var rings = new System.Collections.Generic.List<Transform>();
            var glows = new System.Collections.Generic.List<Renderer>();
            var sparks = new System.Collections.Generic.List<ParticleSystem>();
            foreach (bool left in new[] { true, false })
            {
                string side = left ? "L" : "R";
                Transform shin = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Transform foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                Transform toes = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);

                // Round the ankle, square to the shin, riding the shin so it ignores the foot flexing.
                Vector3 axis = (shin.position - foot.position).normalized;
                var anchor = new GameObject("BootGlow_" + side).transform;
                anchor.SetPositionAndRotation(foot.position + axis * 0.03f, Quaternion.FromToRotation(Vector3.up, axis));
                anchor.SetParent(shin, true);
                rings.Add(GlowPart(anchor, "Ring", ring, core, Vector3.zero, glows));
                rings.Add(GlowPart(anchor, "InnerRing", innerRing, core, new Vector3(0f, 0.06f, 0f), glows));
                GlowPart(anchor, "Halo", halo, glow, Vector3.zero, glows);
                sparks.Add(Sparks(anchor, spark));

                // Under the sole, flat to the ground in the bind pose, then moving with the foot.
                Vector3 heel = Vector3.ProjectOnPlane(foot.position, Vector3.up);
                Vector3 tip = Vector3.ProjectOnPlane(toes.position, Vector3.up);
                var soleGlow = new GameObject("SoleGlow_" + side).transform;
                soleGlow.SetPositionAndRotation(Vector3.Lerp(heel, tip, 0.4f) + Vector3.up * 0.01f, Quaternion.identity);
                soleGlow.SetParent(foot, true);
                GlowPart(soleGlow, "Glow", sole, glow, Vector3.zero, glows);
            }

            var bootsGlow = root.AddComponent<BootsGlow>();
            Set(bootsGlow, "boots", boots);
            var serialized = new SerializedObject(bootsGlow);
            void Fill<T>(string field, System.Collections.Generic.List<T> items) where T : Object
            {
                SerializedProperty list = serialized.FindProperty(field);
                list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("rings", rings);
            Fill("glows", glows);
            Fill("sparks", sparks);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform GlowPart(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition,
            System.Collections.Generic.List<Renderer> glows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            glows.Add(renderer);
            return go.transform;
        }

        /// <summary>Sparks thrown off the ring's rim that drift up the leg and fade; stopped until magnetized.</summary>
        static ParticleSystem Sparks(Transform anchor, Material material)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(anchor, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 32f;

            // A circle in the ring's plane (the shape's circle lies in its XY plane).
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.105f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{EffectsFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Keep the asset (and its GUID); replace its contents.
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>A URP particle material: alpha-blended, or additive (light that adds onto what's behind).</summary>
        static Material GlowMaterial(string name, Color color, bool additive, Texture texture)
        {
            string path = $"{EffectsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A small white dot that fades to transparent at the edge, for sparks.</summary>
        static Texture2D EnsureSoftDotTexture()
        {
            string path = $"{EffectsFolder}/SoftDot.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;
            const int size = 32;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// The player's Animator: a Move blend tree (idle, walk, run by ground speed, run sped up
        /// past its own pace), a MagnetizedMove one (idle and a walk, the deliberate walk from
        /// <see cref="AnimationsFolder"/> if there is one, paced to keep stride with the ground), and an Air state that holds a jump pose picked by AirPhase.
        /// IK is on so <see cref="FootPlanting"/> can pin feet while magnetized.
        /// </summary>
        /// <remarks>Humanoid clip speeds are per unit of body size; <paramref name="humanScale"/> turns them into m/s.</remarks>
        static RuntimeAnimatorController BuildPlayerAnimator(string modelPath, float humanScale,
            out float magnetizedWalkSpeed, out float runClipSpeed)
        {
            AnimationClip ExtraClip(string clipName)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder }))
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (asset is AnimationClip clip && clip.name == clipName)
                            return clip;
                return null;
            }

            AnimationClip Clip(string clipName)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                    if (asset is AnimationClip clip && clip.name == clipName)
                        return clip;
                throw new FileNotFoundException($"No '{clipName}' clip in the player model", modelPath);
            }

            AssetDatabase.DeleteAsset(PlayerAnimatorPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerAnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "MoveScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter("AirPhase", AnimatorControllerParameterType.Float);
            controller.AddParameter("Magnetized", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "WalkScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            AnimationClip idle = Clip("Idle"), walk = Clip("Walk"), run = Clip("Run");
            float walkClipSpeed = walk.averageSpeed.magnitude * humanScale;
            runClipSpeed = run.averageSpeed.magnitude * humanScale;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, walkClipSpeed);
            tree.AddChild(run, runClipSpeed);
            move.speedParameterActive = true;
            move.speedParameter = "MoveScale";

            AnimatorState heavy = controller.CreateBlendTreeInController("MagnetizedMove", out BlendTree heavyTree);
            heavyTree.blendParameter = "Speed";
            heavyTree.useAutomaticThresholds = false;
            AnimationClip heavyWalk = ExtraClip(MagnetizedWalkClip);
            if (heavyWalk == null)
                heavyWalk = walk;
            magnetizedWalkSpeed = heavyWalk.averageSpeed.magnitude * humanScale;
            heavyTree.AddChild(idle, 0f);
            heavyTree.AddChild(heavyWalk, magnetizedWalkSpeed);
            heavy.speedParameterActive = true;
            heavy.speedParameter = "WalkScale";
            Transition(move, heavy, 0.2f, (AnimatorConditionMode.If, "Magnetized"));
            Transition(heavy, move, 0.2f, (AnimatorConditionMode.IfNot, "Magnetized"));

            AnimatorState air = machine.AddState("Air");
            air.motion = Clip("Jump");
            air.timeParameterActive = true;
            air.timeParameter = "AirPhase";

            Transition(move, air, 0.15f, (AnimatorConditionMode.IfNot, "Grounded"));
            Transition(heavy, air, 0.15f, (AnimatorConditionMode.IfNot, "Grounded"));
            Transition(air, move, 0.15f, (AnimatorConditionMode.If, "Grounded"), (AnimatorConditionMode.IfNot, "Magnetized"));
            Transition(air, heavy, 0.15f, (AnimatorConditionMode.If, "Grounded"), (AnimatorConditionMode.If, "Magnetized"));
            machine.defaultState = move;

            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void Transition(AnimatorState from, AnimatorState to, float duration,
            params (AnimatorConditionMode mode, string parameter)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            foreach ((AnimatorConditionMode mode, string parameter) in conditions)
                transition.AddCondition(mode, 0f, parameter);
        }

        // ---------------------------------------------------------------- scene setup

        internal static void SetupLighting()
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

        internal static void BuildCameraAndDebug(GameObject player, CameraSettings cameraSettings)
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
            Set(keys, "boots", player.GetComponent<MagneticBoots>());
            Set(keys, "cameraRig", rig);
            Set(keys, "overlay", overlay);
            Set(overlay, "motor", motor);
            Set(overlay, "controller", controller);
            Set(overlay, "cameraRig", rig);
            Set(overlay, "keys", keys);
            Set(overlay, "boots", player.GetComponent<MagneticBoots>());
        }

        internal static void BuildBootsFeedback(GameObject player, Palette palette)
        {
            var motor = player.GetComponent<PlayerMotor>();
            var boots = player.GetComponent<MagneticBoots>();

            var markerObject = new GameObject("LandingMarker");
            var marker = markerObject.AddComponent<LandingMarker>();
            var ring = Primitive(markerObject.transform, PrimitiveType.Cylinder, "Ring", Vector3.zero, new Vector3(0.9f, 0.01f, 0.9f), palette.Marker);
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var guideObject = new GameObject("Guide");
            guideObject.transform.SetParent(markerObject.transform, false);
            var guide = guideObject.AddComponent<LineRenderer>();
            guide.positionCount = 2;
            guide.widthMultiplier = 0.04f;
            guide.sharedMaterial = palette.Guide;
            guide.shadowCastingMode = ShadowCastingMode.Off;
            guide.useWorldSpace = true;
            Set(marker, "motor", motor);
            Set(marker, "boots", boots);
            Set(marker, "ring", ring.GetComponent<Renderer>());
            Set(marker, "guide", guide);

            var hud = new GameObject("HUD").AddComponent<GravBoots.UI.BootsHud>();
            Set(hud, "boots", boots);
            Set(hud, "landingMarker", marker);
        }

        /// <summary>
        /// The run: a <see cref="LevelSession"/> (start, checkpoints, death and respawn, exit)
        /// and its HUD. Needs the camera rig and debug keys from <see cref="BuildCameraAndDebug"/>.
        /// </summary>
        internal static LevelSession BuildSession(GameObject player)
        {
            var sessionObject = new GameObject("Session");
            var session = sessionObject.AddComponent<LevelSession>();
            Set(session, "player", player.GetComponent<PlayerMotor>());
            Set(session, "cameraRig", Object.FindAnyObjectByType<CameraRig>());
            Set(session, "input", player.GetComponent<PlayerInputReader>());
            var hud = sessionObject.AddComponent<GravBoots.UI.SessionHud>();
            Set(hud, "session", session);
            Set(hud, "health", player.GetComponent<Health>());
            Set(sessionObject.AddComponent<PlaytestLog>(), "session", session);
            var keys = Object.FindAnyObjectByType<DebugGravityKeys>();
            if (keys != null)
                Set(keys, "session", session);
            return session;
        }

        // ---------------------------------------------------------------- areas

        static Transform Area(Transform parent, string name, int index)
        {
            var area = new GameObject(name).transform;
            area.SetParent(parent, false);
            area.localPosition = new Vector3(index * AreaSpacing, 0f, 0f);
            return area;
        }

        static SpawnPoint Spawn(Transform area, string label, int order, Vector3 localPosition, float yaw = 0f)
        {
            var spawn = new GameObject("Spawn").AddComponent<SpawnPoint>();
            spawn.transform.SetParent(area, false);
            spawn.transform.localPosition = localPosition;
            spawn.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            spawn.label = label;
            spawn.order = order;
            return spawn;
        }

        internal static void PointLight(Transform area, Vector3 localPosition, float range, float intensity = 2.5f)
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
        static void Enclosure(Transform parent, Vector3 floorCentre, Vector3 inner, Palette palette, float t = 1f, bool magnetic = false)
        {
            Func<Transform, string, Vector3, Vector3, Material, GameObject> slab = magnetic
                ? (tr, n, c0, size, _) => MagBox(tr, n, c0, size, palette)
                : (tr, n, c0, size, m) => Box(tr, n, c0, size, m);
            Vector3 c = floorCentre + Vector3.up * (inner.y * 0.5f);
            slab(parent, "Floor", floorCentre - Vector3.up * (t * 0.5f), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t), palette.Floor);
            slab(parent, "Ceiling", c + Vector3.up * (inner.y * 0.5f + t * 0.5f), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t), palette.Ceiling);
            slab(parent, "Wall +X", c + Vector3.right * (inner.x * 0.5f + t * 0.5f), new Vector3(t, inner.y, inner.z), palette.Wall);
            slab(parent, "Wall -X", c - Vector3.right * (inner.x * 0.5f + t * 0.5f), new Vector3(t, inner.y, inner.z), palette.Wall);
            slab(parent, "Wall +Z", c + Vector3.forward * (inner.z * 0.5f + t * 0.5f), new Vector3(inner.x, inner.y, t), palette.Wall);
            slab(parent, "Wall -Z", c - Vector3.forward * (inner.z * 0.5f + t * 0.5f), new Vector3(inner.x, inner.y, t), palette.Wall);
        }

        static void BuildCubeRoom(Transform area, Palette p)
        {
            const float s = 16f;
            Enclosure(area, Vector3.zero, Vector3.one * s, p, magnetic: true);
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
            // Discrete quarter-pipe outcrops up onto each wall, 3 m wide. Their open sides are
            // sharp edges: step off and you drop.
            const float h = s * 0.5f, r = QuarterPipeRadius, rampWidth = 3f;
            Curve(area, "Wall Ramp (-X)", new Vector3(-h + r, 0f, 4f), Vector3.left, Vector3.up, CurvedRamp.Shape.Concave, r, rampWidth, p);
            Curve(area, "Wall Ramp (+Z)", new Vector3(-3f, 0f, h - r), Vector3.forward, Vector3.up, CurvedRamp.Shape.Concave, r, rampWidth, p);
            Curve(area, "Wall Ramp (-Z)", new Vector3(4f, 0f, -h + r), Vector3.back, Vector3.up, CurvedRamp.Shape.Concave, r, rampWidth, p);
            Curve(area, "Wall Ramp (+X)", new Vector3(h - r, 0f, 4f), Vector3.right, Vector3.up, CurvedRamp.Shape.Concave, r, rampWidth, p);
            PointLight(area, new Vector3(0f, s * 0.5f, 0f), 30f, 3f);
            Spawn(area, "Cube Room: E for boots, then up any wall ramp; the room's corners stay sharp", 0,
                new Vector3(0f, 0.05f, -2f));
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

            // The shaft below; its lower part is a death pit.
            float sy = -t - depth * 0.5f;
            Box(area, "Shaft +X", new Vector3(hole * 0.5f + 0.25f, sy, 0f), new Vector3(0.5f, depth, hole), p.Dark);
            Box(area, "Shaft -X", new Vector3(-hole * 0.5f - 0.25f, sy, 0f), new Vector3(0.5f, depth, hole), p.Dark);
            Box(area, "Shaft +Z", new Vector3(0f, sy, hole * 0.5f + 0.25f), new Vector3(hole + 1f, depth, 0.5f), p.Dark);
            Box(area, "Shaft -Z", new Vector3(0f, sy, -hole * 0.5f - 0.25f), new Vector3(hole + 1f, depth, 0.5f), p.Dark);
            Box(area, "Shaft Bottom", new Vector3(0f, -t - depth - 0.25f, 0f), new Vector3(hole + 1f, 0.5f, hole + 1f), p.Orange)
                .tag = Layers.LethalTag;
            KillZone(area, "Shaft Kill Volume", new Vector3(0f, -t - depth + 4f, 0f), new Vector3(hole, 8f, hole));

            PointLight(area, new Vector3(0f, h * 0.6f, 0f), 25f, 3f);
            PointLight(area, new Vector3(0f, -t - depth * 0.5f, 0f), 10f, 1.5f);
            Spawn(area, "Pit Under Ceiling: press 2, walk over the pit, look down", 5, new Vector3(0f, 0.05f, -7f));
        }

        /// <summary>
        /// Phase 1B: setups for the magnetic boots. Magnetic surfaces are the bright panels;
        /// grey and dark surfaces are not magnetic. Each setup has its own spawn point.
        /// </summary>
        static void BuildBootsGym(Transform area, Palette p)
        {
            // Ground everywhere except the Pit Traverse's pit (x ±5, z ±6 around the origin), 5 cm
            // below the setups' floors so the magnetic floors and pads are always what you stand on.
            Box(area, "Ground West", new Vector3(-20f, -0.55f, 0f), new Vector3(30f, 1f, 60f), p.Floor);
            Box(area, "Ground East", new Vector3(40.5f, -0.55f, 0f), new Vector3(71f, 1f, 60f), p.Floor);
            Box(area, "Ground North", new Vector3(0f, -0.55f, 18f), new Vector3(10f, 1f, 24f), p.Floor);
            Box(area, "Ground South", new Vector3(0f, -0.55f, -18f), new Vector3(10f, 1f, 24f), p.Floor);

            BuildLoopRoom(Sub(area, "Loop Room", new Vector3(-20f, 0f, 0f)), p);
            BuildPitTraverse(Sub(area, "Pit Traverse", new Vector3(0f, 0f, 0f)), p);
            BuildBlockCorners(Sub(area, "Block Corners", new Vector3(20f, 0f, 0f)), p);
            BuildPipe(Sub(area, "Pipe", new Vector3(40f, 0f, 0f)), p);
            BuildSeams(Sub(area, "Seams", new Vector3(62f, 0f, 0f)), p);
        }

        static Transform Sub(Transform area, string name, Vector3 localPosition)
        {
            var sub = new GameObject(name).transform;
            sub.SetParent(area, false);
            sub.localPosition = localPosition;
            return sub;
        }

        /// <summary>
        /// Floor → end wall → ceiling → start wall → floor, joined by quarter-pipes. The room's
        /// own corners are sharp, so the curves are the only way round.
        /// </summary>
        static void BuildLoopRoom(Transform room, Palette p)
        {
            Vector3 inner = new Vector3(6f, 6f, 18f);
            const float r = QuarterPipeRadius;
            float halfZ = inner.z * 0.5f;
            Enclosure(room, Vector3.zero, inner, p, 0.5f, magnetic: true);

            Curve(room, "Curve Floor→End Wall", new Vector3(0f, 0f, halfZ - r), Vector3.forward, Vector3.up, CurvedRamp.Shape.Concave, r, inner.x, p);
            Curve(room, "Curve End Wall→Ceiling", new Vector3(0f, inner.y - r, halfZ), Vector3.up, Vector3.back, CurvedRamp.Shape.Concave, r, inner.x, p);
            Curve(room, "Curve Ceiling→Start Wall", new Vector3(0f, inner.y, -halfZ + r), Vector3.back, Vector3.down, CurvedRamp.Shape.Concave, r, inner.x, p);
            Curve(room, "Curve Start Wall→Floor", new Vector3(0f, r, -halfZ), Vector3.down, Vector3.forward, CurvedRamp.Shape.Concave, r, inner.x, p);

            PointLight(room, new Vector3(0f, 3f, -4f), 14f, 2f);
            PointLight(room, new Vector3(0f, 3f, 4f), 14f, 2f);
            Spawn(room, "Boots: Loop Room. E on, then just keep walking: the curves take you round", 10, new Vector3(0f, 0.05f, -4f));
        }

        /// <summary>A pit you can only cross by walls or ceiling; the bottom is marked lethal.</summary>
        static void BuildPitTraverse(Transform room, Palette p)
        {
            const float w = 10f, h = 7f, len = 24f, pit = 12f, depth = 10f, t = 0.5f;
            float floorLen = (len - pit) * 0.5f;
            float floorZ = pit * 0.5f + floorLen * 0.5f;
            MagBox(room, "Floor Start", new Vector3(0f, -t * 0.5f, -floorZ), new Vector3(w, t, floorLen), p);
            MagBox(room, "Floor End", new Vector3(0f, -t * 0.5f, floorZ), new Vector3(w, t, floorLen), p);
            MagBox(room, "Ceiling", new Vector3(0f, h + t * 0.5f, 0f), new Vector3(w + 2 * t, t, len + 2 * t), p);
            MagBox(room, "Wall +X", new Vector3(w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, len), p);
            MagBox(room, "Wall -X", new Vector3(-w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, len), p);
            MagBox(room, "Wall Start", new Vector3(0f, h * 0.5f, -len * 0.5f - t * 0.5f), new Vector3(w + 2 * t, h, t), p);
            MagBox(room, "Wall End", new Vector3(0f, h * 0.5f, len * 0.5f + t * 0.5f), new Vector3(w + 2 * t, h, t), p);

            // The pit: dark (non-magnetic) shaft walls, lethal bottom (kills from Phase 2 on).
            float sy = -depth * 0.5f;
            Box(room, "Pit +X", new Vector3(w * 0.5f + t * 0.5f, sy, 0f), new Vector3(t, depth, pit), p.Dark);
            Box(room, "Pit -X", new Vector3(-w * 0.5f - t * 0.5f, sy, 0f), new Vector3(t, depth, pit), p.Dark);
            Box(room, "Pit Start", new Vector3(0f, sy - t * 0.5f, -pit * 0.5f - t * 0.5f), new Vector3(w, depth - t, t), p.Dark);
            Box(room, "Pit End", new Vector3(0f, sy - t * 0.5f, pit * 0.5f + t * 0.5f), new Vector3(w, depth - t, t), p.Dark);
            GameObject bottom = Box(room, "Pit Bottom (lethal)", new Vector3(0f, -depth - t * 0.5f, 0f), new Vector3(w, t, pit), p.Orange);
            bottom.tag = Layers.LethalTag;
            KillZone(room, "Pit Kill Volume", new Vector3(0f, -depth + 4f, 0f), new Vector3(w, 8f, pit));

            const float r = QuarterPipeRadius;
            float halfLen = len * 0.5f;
            Curve(room, "Curve Floor→Start Wall", new Vector3(0f, 0f, -halfLen + r), Vector3.back, Vector3.up, CurvedRamp.Shape.Concave, r, w, p);
            Curve(room, "Curve Start Wall→Ceiling", new Vector3(0f, h - r, -halfLen), Vector3.up, Vector3.forward, CurvedRamp.Shape.Concave, r, w, p);
            Curve(room, "Curve Ceiling→End Wall", new Vector3(0f, h, halfLen - r), Vector3.forward, Vector3.down, CurvedRamp.Shape.Concave, r, w, p);
            Curve(room, "Curve End Wall→Floor", new Vector3(0f, r, halfLen), Vector3.down, Vector3.back, CurvedRamp.Shape.Concave, r, w, p);

            PointLight(room, new Vector3(0f, h - 1f, -8f), 14f, 2f);
            PointLight(room, new Vector3(0f, h - 1f, 8f), 14f, 2f);
            PointLight(room, new Vector3(0f, -depth * 0.5f, 0f), 12f, 1.5f);
            Spawn(room, "Boots: Pit Traverse. Up the curve behind you and across the ceiling; drop over the pit to see the red marker", 11,
                new Vector3(0f, 0.05f, -7f), 180f);
        }

        /// <summary>
        /// Curves both ways: a quarter-pipe from the pad up the block's face, rounded edges over
        /// its top and down the far side, and a quarter-pipe onto the far pad. The small block
        /// beside it has sharp edges, which the boots can't take.
        /// </summary>
        static void BuildBlockCorners(Transform setup, Palette p)
        {
            const float width = 6f, edge = 1.5f, foot = QuarterPipeRadius;
            MagBox(setup, "Pad Front", new Vector3(0f, 0.05f, -3f), new Vector3(8f, 0.1f, 8f), p);
            MagBox(setup, "Pad Back", new Vector3(0f, 0.05f, 11f), new Vector3(8f, 0.1f, 8f), p);
            // A 6 m block (front face z = 1, back face z = 7) whose top edges are rounded.
            MagBox(setup, "Block Lower", new Vector3(0f, (6f - edge) * 0.5f, 4f), new Vector3(width, 6f - edge, 6f), p);
            MagBox(setup, "Block Top", new Vector3(0f, 6f - edge * 0.5f, 4f), new Vector3(width, edge, 6f - 2f * edge), p);
            Curve(setup, "Edge Front", new Vector3(0f, 6f, 1f + edge), Vector3.back, Vector3.up, CurvedRamp.Shape.Convex, edge, width, p);
            Curve(setup, "Edge Back", new Vector3(0f, 6f, 7f - edge), Vector3.forward, Vector3.up, CurvedRamp.Shape.Convex, edge, width, p);
            Curve(setup, "Curve Pad→Front", new Vector3(0f, 0.1f, 1f - foot), Vector3.forward, Vector3.up, CurvedRamp.Shape.Concave, foot, width, p);
            Curve(setup, "Curve Back→Pad", new Vector3(0f, 0.1f + foot, 7f), Vector3.down, Vector3.forward, CurvedRamp.Shape.Concave, foot, width, p);

            MagBox(setup, "Sharp Block 2 m", new Vector3(-7f, 1f, 4f), new Vector3(2f, 2f, 2f), p);
            Spawn(setup, "Boots: Block Corners. Curves up, over and down; the small sharp block can't be climbed", 12,
                new Vector3(0f, 0.15f, -5f));
        }

        /// <summary>A 16-sided tube: walk sideways around the inside on continuous seams.</summary>
        static void BuildPipe(Transform setup, Palette p)
        {
            const int sides = 16;
            const float radius = 3.5f, thickness = 0.5f, length = 14f;
            float centreY = radius + 0.25f;
            float width = 2f * radius * Mathf.Tan(Mathf.PI / sides) + 0.08f;
            for (int i = 0; i < sides; i++)
            {
                float theta = i * 360f / sides;
                float rad = theta * Mathf.Deg2Rad;
                float d = radius + thickness * 0.5f;
                var centre = new Vector3(Mathf.Sin(rad) * d, centreY - Mathf.Cos(rad) * d, 0f);
                MagBox(setup, $"Pipe {i:00}", centre, new Vector3(width, thickness, length), p, Quaternion.Euler(0f, 0f, theta));
            }
            MagBox(setup, "Pad", new Vector3(0f, 0.05f, -length * 0.5f - 3f), new Vector3(4f, 0.1f, 6f), p);
            PointLight(setup, new Vector3(0f, centreY, 0f), 12f, 2f);
            Spawn(setup, "Boots: Pipe. E on the pad, walk in, then strafe around the inside", 13,
                new Vector3(0f, 0.15f, -length * 0.5f - 4f));
        }

        /// <summary>Magnetic walls with non-magnetic sections: walking onto one drops you.</summary>
        static void BuildSeams(Transform setup, Palette p)
        {
            const float h = 6f, t = 0.5f, z = 4f;
            MagBox(setup, "Pad", new Vector3(0f, 0.05f, 1f), new Vector3(14f, 0.1f, 5f), p);
            Curve(setup, "Curve Pad→Wall", new Vector3(0f, 0.1f, z - t * 0.5f - QuarterPipeRadius), Vector3.forward, Vector3.up,
                CurvedRamp.Shape.Concave, QuarterPipeRadius, 14f, p);
            MagBox(setup, "Wall Left (magnetic)", new Vector3(-4.5f, h * 0.5f, z), new Vector3(5f, h, t), p);
            Box(setup, "Wall Gap (not magnetic)", new Vector3(0f, h * 0.5f, z), new Vector3(4f, h, t), p.Dark);
            MagBox(setup, "Wall Right Lower (magnetic)", new Vector3(4.5f, 1.5f, z), new Vector3(5f, 3f, t), p);
            Box(setup, "Wall Right Upper (not magnetic)", new Vector3(4.5f, 4.5f, z), new Vector3(5f, 3f, t), p.Dark);
            Spawn(setup, "Boots: Seams. Up the curve, then onto the dark sections and you drop", 14,
                new Vector3(-4.5f, 0.15f, 0f));
        }

        /// <summary>
        /// Phase 2: a short run with a checkpoint on a wall over a death pit. Fall in on purpose
        /// to see the respawn put you back on the wall, magnetized; the exit pad is past the pit.
        /// </summary>
        static SpawnPoint BuildCheckpointRun(Transform area, Palette p)
        {
            const float r = QuarterPipeRadius;
            MagBox(area, "Floor Start", new Vector3(0f, -0.25f, -7f), new Vector3(10f, 0.5f, 10f), p);
            MagBox(area, "Floor End", new Vector3(0f, -0.25f, 10f), new Vector3(10f, 0.5f, 8f), p);
            MagBox(area, "Wall", new Vector3(5.25f, -1f, 1f), new Vector3(0.5f, 14f, 26f), p);
            Curve(area, "Curve Floor→Wall", new Vector3(5f - r, 0f, -7f), Vector3.right, Vector3.up, CurvedRamp.Shape.Concave, r, 6f, p);
            Curve(area, "Curve Wall→Floor", new Vector3(5f, r, 10f), Vector3.down, Vector3.left, CurvedRamp.Shape.Concave, r, 6f, p);

            // The pit: dark sides, lethal bottom, and a kill volume in its lower part.
            Box(area, "Pit -X", new Vector3(-5.25f, -4f, 2f), new Vector3(0.5f, 8f, 8f), p.Dark);
            Box(area, "Pit Start", new Vector3(0f, -4.25f, -2.25f), new Vector3(10f, 7.5f, 0.5f), p.Dark);
            Box(area, "Pit End", new Vector3(0f, -4.25f, 6.25f), new Vector3(10f, 7.5f, 0.5f), p.Dark);
            Box(area, "Pit Bottom (lethal)", new Vector3(0f, -8.25f, 2f), new Vector3(10f, 0.5f, 8f), p.Orange).tag = Layers.LethalTag;
            KillZone(area, "Pit Kill Volume", new Vector3(0f, -5f, 2f), new Vector3(10f, 6f, 8f));

            CheckpointPad(area, "Checkpoint (wall)", new Vector3(5f, 3f, 2f), Vector3.forward, Vector3.left, p, 16,
                new Vector3(6f, 2f, 4f));
            ExitPad(area, "Exit", new Vector3(-2f, 0f, 11f), Vector3.forward, Vector3.up, p);

            PointLight(area, new Vector3(0f, 4f, -6f), 16f, 2f);
            PointLight(area, new Vector3(0f, 4f, 8f), 16f, 2f);
            PointLight(area, new Vector3(0f, -4f, 2f), 10f, 1.5f);
            return Spawn(area, "Checkpoint Run: up the wall curve, cross the wall checkpoint, then drop into the pit to respawn on the wall", 15,
                new Vector3(0f, 0.05f, -10f));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A checkpoint standing on a surface facing <paramref name="up"/>, with a pad that lights when active.</summary>
        internal static Checkpoint CheckpointPad(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up,
            Palette palette, int order, Vector3? triggerSize = null)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(forward, up);
            // Added first: its Reset sets a default trigger, which the size below replaces.
            var checkpoint = go.AddComponent<Checkpoint>();
            var trigger = go.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            Vector3 size = triggerSize ?? new Vector3(3f, 2f, 3f);
            trigger.size = size;
            trigger.center = new Vector3(0f, size.y * 0.5f, 0f);
            checkpoint.label = name;
            checkpoint.order = order;
            var pad = Primitive(go.transform, PrimitiveType.Cylinder, "Pad", new Vector3(0f, 0.01f, 0f), new Vector3(1.6f, 0.01f, 1.6f), palette.CheckpointIdle);
            pad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Set(checkpoint, "pad", pad.GetComponent<Renderer>());
            Set(checkpoint, "idleMaterial", palette.CheckpointIdle);
            Set(checkpoint, "litMaterial", palette.CheckpointLit);
            return checkpoint;
        }

        /// <summary>The chamber's exit: a glowing pad with a trigger above it.</summary>
        internal static LevelExit ExitPad(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up, Palette palette)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(forward, up);
            var exit = go.AddComponent<LevelExit>();
            var trigger = go.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2f, 3f);
            trigger.center = new Vector3(0f, 1f, 0f);
            var pad = Primitive(go.transform, PrimitiveType.Cylinder, "Pad", new Vector3(0f, 0.01f, 0f), new Vector3(2.4f, 0.01f, 2.4f), palette.Exit);
            pad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return exit;
        }

        /// <summary>
        /// A placeholder laser turret standing on a surface facing <paramref name="up"/>, resting
        /// aim along <paramref name="forward"/>: a dark base (solid), a head that turns, and the
        /// laser line.
        /// </summary>
        internal static Turret TurretMount(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up, Palette palette)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(forward, up);
            var turret = go.AddComponent<Turret>();

            Box(go.transform, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.4f, 0.8f), palette.Dark);
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 0.6f, 0f);
            Primitive(head, PrimitiveType.Sphere, "Dome", Vector3.zero, Vector3.one * 0.55f, palette.Dark);
            Primitive(head, PrimitiveType.Cube, "Barrel", new Vector3(0f, 0f, 0.35f), new Vector3(0.12f, 0.12f, 0.45f), palette.Dark);
            Primitive(head, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0.05f, 0.24f), Vector3.one * 0.14f, palette.TurretEye);

            var line = new GameObject("Laser").AddComponent<LineRenderer>();
            line.transform.SetParent(go.transform, false);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = palette.Guide;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            Set(turret, "head", head);
            Set(turret, "beam", line);
            return turret;
        }

        /// <summary>
        /// A sentry standing on a surface facing <paramref name="up"/>, first looking along
        /// <paramref name="forward"/>: a solid body you can land on, a head that turns, its
        /// searchlight, and the red line where its firing plane meets the room.
        /// </summary>
        internal static Sentry SentryMount(Transform parent, string name, Vector3 localPosition, Vector3 forward, Vector3 up, Palette palette)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(forward, up);
            var sentry = go.AddComponent<Sentry>();

            // Solid up to the head's top (1.3 m), which is what a stomp lands on.
            var body = Box(go.transform, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(0.9f, 1.3f, 0.9f), palette.SentryShell);
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 1f, 0f);
            Primitive(head, PrimitiveType.Cube, "Visor", new Vector3(0f, 0f, 0.42f), new Vector3(0.6f, 0.16f, 0.1f), palette.Dark);
            var eye = Primitive(head, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0f, 0.48f), Vector3.one * 0.14f, palette.TurretEye);

            var sight = Line(go.transform, "Sight", palette);
            var danger = Line(go.transform, "Danger Line", palette);
            danger.startWidth = danger.endWidth = 0.05f;
            danger.startColor = danger.endColor = new Color(1f, 0.2f, 0.1f, 0.6f);
            danger.enabled = true;

            Set(sentry, "head", head);
            Set(sentry, "body", body.transform);
            Set(sentry, "sight", sight);
            Set(sentry, "dangerLine", danger);
            Set(sentry, "eye", eye.GetComponent<Renderer>());
            Set(sentry, "eyeOff", palette.Dark);
            return sentry;
        }

        static LineRenderer Line(Transform parent, string name, Palette palette)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = palette.Guide;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// <summary>
        /// A <see cref="KinematicMover"/> with its path points (local poses under the returned
        /// root, which sits at the first point) and a platform: a deck of <paramref name="size"/>
        /// whose top face is the platform's origin, so a point's up is the deck's walking side.
        /// </summary>
        internal static KinematicMover MovingPlatform(Transform parent, string name, Vector3 localStart, Quaternion localRotation,
            Vector3[] offsets, Vector3 size, bool magnetic, Palette palette, MoverMode mode = MoverMode.PingPong,
            float speed = 2f, float wait = 1f)
        {
            KinematicMover mover = Track(parent, name, localStart, localRotation, offsets, null, mode, speed, wait);
            var platform = new GameObject("Platform").transform;
            platform.SetParent(mover.transform, false);
            if (magnetic)
                MagBox(platform, "Deck", new Vector3(0f, -size.y * 0.5f, 0f), size, palette);
            else
                Box(platform, "Deck", new Vector3(0f, -size.y * 0.5f, 0f), size, palette.Blue);
            mover.platform = platform;
            mover.CachePath();
            mover.Evaluate(0f);
            return mover;
        }

        /// <summary>A sentry riding a track: the sentry itself is the mover's platform.</summary>
        internal static KinematicMover TrackedSentry(Transform parent, string name, Vector3 localStart, Quaternion localRotation,
            Vector3[] offsets, Quaternion[] rotations, Palette palette, float speed = 2f, float wait = 1f)
        {
            KinematicMover mover = Track(parent, name, localStart, localRotation, offsets, rotations, MoverMode.PingPong, speed, wait);
            Sentry sentry = SentryMount(mover.transform, "Sentry", Vector3.zero, Vector3.forward, Vector3.up, palette);
            mover.platform = sentry.transform;
            mover.CachePath();
            mover.Evaluate(0f);
            return mover;
        }

        /// <summary>
        /// The mover and its points. <paramref name="offsets"/> are in the root's frame; each
        /// point takes the root's rotation unless <paramref name="rotations"/> (also in the
        /// root's frame) says otherwise.
        /// </summary>
        static KinematicMover Track(Transform parent, string name, Vector3 localStart, Quaternion localRotation,
            Vector3[] offsets, Quaternion[] rotations, MoverMode mode, float speed, float wait)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localStart;
            root.localRotation = localRotation;
            var mover = root.gameObject.AddComponent<KinematicMover>();
            mover.points = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject($"Point {i + 1}").transform;
                point.SetParent(root, false);
                point.localPosition = offsets[i];
                point.localRotation = rotations != null && i < rotations.Length ? rotations[i] : Quaternion.identity;
                mover.points[i] = point;
            }
            mover.mode = mode;
            mover.speed = speed;
            mover.wait = wait;
            return mover;
        }

        /// <summary>An invisible box that kills on entry (inside death pits).</summary>
        internal static KillVolume KillZone(Transform parent, string name, Vector3 localCentre, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            var volume = go.AddComponent<KillVolume>();
            volume.size = size;
            volume.Rebuild();
            return volume;
        }


        internal static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material,
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

        /// <summary>A box on the Magnetic layer with the magnetic panel material.</summary>
        internal static GameObject MagBox(Transform parent, string name, Vector3 localCentre, Vector3 size, Palette palette,
            Quaternion? localRotation = null)
        {
            GameObject go = Box(parent, name, localCentre, size, palette.Magnetic, localRotation);
            go.layer = Layers.Magnetic;
            return go;
        }

        /// <summary>
        /// A magnetic 90° curve starting at <paramref name="start"/> on a surface facing
        /// <paramref name="up"/>, heading along <paramref name="forward"/> (see <see cref="CurvedRamp"/>).
        /// </summary>
        internal static GameObject Curve(Transform parent, string name, Vector3 start, Vector3 forward, Vector3 up,
            CurvedRamp.Shape shape, float radius, float width, Palette palette)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(CurvedRamp));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = start;
            go.transform.localRotation = Quaternion.LookRotation(forward, up);
            go.layer = Layers.Magnetic;
            var ramp = go.GetComponent<CurvedRamp>();
            ramp.shape = shape;
            ramp.radius = radius;
            ramp.width = width;
            ramp.segments = Mathf.Max(8, Mathf.CeilToInt(radius * 8f));
            ramp.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = palette.Magnetic;
            return go;
        }

        static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
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
