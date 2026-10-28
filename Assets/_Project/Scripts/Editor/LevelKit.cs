using System.IO;
using GravBoots.Cameras;
using GravBoots.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// Operations behind the Level Kit window: new chamber scenes, placing pieces, and quick
    /// edits on the selection. Everything is undoable. Separate from the window so it can be
    /// driven from code (tests, the CLI) as well as buttons.
    /// </summary>
    public static class LevelKit
    {
        public const string LevelsFolder = TestBedBuilder.Root + "/Levels";
        const string RootName = "Level";
        const string GridStepKey = "GravBoots.LevelKit.GridStep";

        public enum Piece { PlainBlock, MagneticBlock, LethalFloor, QuarterPipe, RoundedEdge, Spawn, Checkpoint, KillVolume, Exit, Turret, Sentry, MovingPlatform, TrackedSentry, StartElevator, ExitElevator }

        /// <summary>Grid size for snapping and resizing (metres), remembered per user.</summary>
        public static float GridStep
        {
            get => EditorPrefs.GetFloat(GridStepKey, 0.5f);
            set
            {
                EditorPrefs.SetFloat(GridStepKey, Mathf.Max(0.05f, value));
                ApplySnapSettings();
            }
        }

        /// <summary>Turns on Unity's own grid snapping for the Move tool, at the kit's step, and 90° rotation steps.</summary>
        public static void ApplySnapSettings()
        {
            EditorSnapSettings.gridSnapEnabled = true;
            EditorSnapSettings.move = Vector3.one * GridStep;
            EditorSnapSettings.rotate = 90f;
        }

        // ------------------------------------------------------------ chambers

        [MenuItem("GravBoots/New Chamber…")]
        static void NewChamberMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(LevelsFolder);
            string path = EditorUtility.SaveFilePanelInProject("New Chamber", "Chamber_01", "unity",
                "Name the new chamber scene.", LevelsFolder);
            if (!string.IsNullOrEmpty(path))
                NewChamber(path);
        }

        /// <summary>
        /// Creates and opens a playable chamber scene: lighting, player, camera, HUD, a magnetic
        /// starter floor, and an example wall with a quarter-pipe up onto it, ready to copy.
        /// </summary>
        public static void NewChamber(string path)
        {
            var palette = TestBedBuilder.EnsureMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // A new scene unloads unreferenced assets, so load what we need after it.
            palette = TestBedBuilder.EnsureMaterials();
            var cameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(TestBedBuilder.SettingsFolder + "/CameraSettings.asset");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestBedBuilder.PlayerPrefabPath);

            TestBedBuilder.SetupLighting();
            Transform root = new GameObject(RootName).transform;
            TestBedBuilder.MagBox(root, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(12f, 0.5f, 12f), palette);
            TestBedBuilder.MagBox(root, "Wall", new Vector3(0f, 3f, 6.25f), new Vector3(12f, 6f, 0.5f), palette);
            float r = TestBedBuilder.QuarterPipeRadius;
            TestBedBuilder.Curve(root, "Quarter-Pipe", new Vector3(0f, 0f, 6f - r), Vector3.forward, Vector3.up,
                CurvedRamp.Shape.Concave, r, 3f, palette);
            AddSpawn(root, new Vector3(0f, 0.05f, -3f), "Start");

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -3f), Quaternion.identity);
            TestBedBuilder.BuildCameraAndDebug(player, cameraSettings);
            TestBedBuilder.BuildBootsFeedback(player, palette);
            TestBedBuilder.BuildSession(player);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = root.gameObject;
            ApplySnapSettings();
        }

        // ------------------------------------------------------------ placing pieces

        /// <summary>
        /// Adds a piece at the Scene view's focus point (snapped to the grid), parented under
        /// the chamber's "Level" object, and selects it.
        /// </summary>
        public static GameObject Place(Piece piece) => Place(piece, FocusPoint());

        public static GameObject Place(Piece piece, Vector3 position)
        {
            var palette = TestBedBuilder.EnsureMaterials();
            Transform root = LevelRoot();
            Vector3 at = SnapPoint(position);
            GameObject go;
            switch (piece)
            {
                case Piece.PlainBlock:
                    go = TestBedBuilder.Box(root, "Block", Vector3.zero, new Vector3(2f, 2f, 2f), palette.Wall);
                    at.y = Mathf.Max(at.y, 0f) + 1f;
                    break;
                case Piece.MagneticBlock:
                    go = TestBedBuilder.MagBox(root, "Magnetic Block", Vector3.zero, new Vector3(2f, 2f, 2f), palette);
                    at.y = Mathf.Max(at.y, 0f) + 1f;
                    break;
                case Piece.LethalFloor:
                    go = TestBedBuilder.Box(root, "Lethal Floor", Vector3.zero, new Vector3(4f, 0.5f, 4f), palette.Orange);
                    go.tag = Layers.LethalTag;
                    at.y -= 0.25f;
                    break;
                case Piece.QuarterPipe:
                    go = TestBedBuilder.Curve(root, "Quarter-Pipe", Vector3.zero, Vector3.forward, Vector3.up,
                        CurvedRamp.Shape.Concave, TestBedBuilder.QuarterPipeRadius, 3f, palette);
                    break;
                case Piece.RoundedEdge:
                    go = TestBedBuilder.Curve(root, "Rounded Edge", Vector3.zero, Vector3.forward, Vector3.up,
                        CurvedRamp.Shape.Convex, 1.5f, 3f, palette);
                    break;
                case Piece.Checkpoint:
                    go = TestBedBuilder.CheckpointPad(root, $"Checkpoint {NextSpawnOrder()}", Vector3.zero, Vector3.forward, Vector3.up,
                        palette, NextSpawnOrder()).gameObject;
                    break;
                case Piece.KillVolume:
                    go = TestBedBuilder.KillZone(root, "Kill Volume", Vector3.zero, new Vector3(4f, 2f, 4f)).gameObject;
                    at.y -= 1f;
                    break;
                case Piece.Exit:
                    go = TestBedBuilder.ExitPad(root, "Exit", Vector3.zero, Vector3.forward, Vector3.up, palette).gameObject;
                    break;
                case Piece.Sentry:
                    go = TestBedBuilder.SentryMount(root, "Sentry", Vector3.zero, Vector3.forward, Vector3.up, palette).gameObject;
                    break;
                case Piece.MovingPlatform:
                    go = TestBedBuilder.MovingPlatform(root, "Moving Platform", Vector3.zero, Quaternion.identity,
                        new[] { Vector3.zero, new Vector3(0f, 0f, 6f) }, new Vector3(3f, 0.3f, 3f), true, palette).gameObject;
                    break;
                case Piece.TrackedSentry:
                    go = TestBedBuilder.TrackedSentry(root, "Sentry on Track", Vector3.zero, Quaternion.identity,
                        new[] { Vector3.zero, new Vector3(0f, 0f, 6f) }, null, palette).gameObject;
                    break;
                case Piece.StartElevator:
                {
                    Elevator start = ElevatorBuilder.Build(root, "Start Elevator", Vector3.zero, Vector3.forward, Vector3.up,
                        ElevatorRole.Start, palette, spawnOrder: -1);
                    go = start.gameObject;
                    // The run starts here from now on.
                    var session = Object.FindAnyObjectByType<LevelSession>();
                    if (session != null)
                    {
                        var serialized = new SerializedObject(session);
                        serialized.FindProperty("startSpawn").objectReferenceValue = start.boardingSpawn;
                        serialized.ApplyModifiedProperties();
                    }
                    break;
                }
                case Piece.ExitElevator:
                    go = ElevatorBuilder.Build(root, "Exit Elevator", Vector3.zero, Vector3.forward, Vector3.up,
                        ElevatorRole.Exit, palette).gameObject;
                    break;
                case Piece.Turret:
                    go = TestBedBuilder.TurretMount(root, "Turret", Vector3.zero, Vector3.forward, Vector3.up, palette).gameObject;
                    break;
                default:
                    int order = NextSpawnOrder();
                    go = AddSpawn(root, at + Vector3.up * 0.05f, $"Spawn {order}", order).gameObject;
                    at = go.transform.position;
                    break;
            }
            go.transform.position = at;
            Undo.RegisterCreatedObjectUndo(go, $"Add {piece}");
            Selection.activeGameObject = go;
            return go;
        }

        // Numbered after the existing spawns and checkpoints, so "Start" stays first and N cycles in order.
        static int NextSpawnOrder() => Object.FindObjectsByType<SpawnPoint>().Length;

        static SpawnPoint AddSpawn(Transform root, Vector3 position, string label, int order = 0)
        {
            var spawn = new GameObject(label).AddComponent<SpawnPoint>();
            spawn.transform.SetParent(root, false);
            spawn.transform.position = position;
            spawn.label = label;
            spawn.order = order;
            return spawn;
        }

        static Transform LevelRoot()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Add Level Root");
            }
            return root.transform;
        }

        static Vector3 FocusPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        static Vector3 SnapPoint(Vector3 p)
        {
            float step = GridStep;
            return new Vector3(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step, Mathf.Round(p.z / step) * step);
        }

        /// <summary>
        /// Gives a chamber made before Phase 2 its game loop (session and HUD), so it gets
        /// death, checkpoints and the exit. Does nothing if it already has one.
        /// </summary>
        [MenuItem("GravBoots/Add Game Loop to Open Chamber")]
        public static void AddGameLoop()
        {
            var existing = Object.FindAnyObjectByType<LevelSession>();
            if (existing != null)
            {
                // Chambers made before the playtest log get just that.
                if (existing.GetComponent<GravBoots.Debugging.PlaytestLog>() == null)
                {
                    var log = Undo.AddComponent<GravBoots.Debugging.PlaytestLog>(existing.gameObject);
                    var serialized = new SerializedObject(log);
                    serialized.FindProperty("session").objectReferenceValue = existing;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                    Debug.Log("[Level Kit] Added the playtest log to this chamber.");
                }
                else
                    Debug.Log("[Level Kit] This chamber already has a game loop.");
                return;
            }
            var player = Object.FindAnyObjectByType<GravBoots.Player.PlayerMotor>();
            if (player == null)
            {
                Debug.LogWarning("[Level Kit] No player in the open scene.");
                return;
            }
            LevelSession session = TestBedBuilder.BuildSession(player.gameObject);
            Undo.RegisterCreatedObjectUndo(session.gameObject, "Add Game Loop");
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }

        // ------------------------------------------------------------ mover paths

        /// <summary>Adds a point to a mover's path, <paramref name="distance"/> on along the last point's forward.</summary>
        public static Transform AddPoint(KinematicMover mover, float distance = 3f)
        {
            Transform last = LastPoint(mover);
            Vector3 position = last != null ? last.position + last.forward * distance : mover.transform.position;
            Quaternion rotation = last != null ? last.rotation : mover.transform.rotation;
            return AppendPoint(mover, position, rotation);
        }

        /// <summary>
        /// Continues a mover's path round a 90° curve from its last point (heading along that
        /// point's forward, standing on its up): concave climbs onto the wall ahead, convex
        /// goes over an edge. Matches a quarter-pipe or rounded edge of the same radius.
        /// </summary>
        public static void AddArc(KinematicMover mover, float radius, bool concave, int segments = 8)
        {
            Transform last = LastPoint(mover);
            if (last == null)
                return;
            Vector3 start = last.position, forward = last.forward, up = last.up;
            float sign = concave ? 1f : -1f;
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.PI * 0.5f * i / segments;
                float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
                Vector3 position = start + forward * (radius * s) + up * (sign * radius * (1f - c));
                Vector3 normal = up * c - forward * (sign * s);
                Vector3 travel = forward * c + up * (sign * s);
                AppendPoint(mover, position, Quaternion.LookRotation(travel, normal));
            }
        }

        public static void RemoveLastPoint(KinematicMover mover)
        {
            Transform last = LastPoint(mover);
            if (last == null || mover.points.Length <= 1)
                return;
            Undo.RecordObject(mover, "Remove Point");
            var points = new Transform[mover.points.Length - 1];
            System.Array.Copy(mover.points, points, points.Length);
            mover.points = points;
            Undo.DestroyObjectImmediate(last.gameObject);
            mover.CachePath();
        }

        static Transform LastPoint(KinematicMover mover)
        {
            for (int i = mover.points.Length - 1; i >= 0; i--)
                if (mover.points[i] != null)
                    return mover.points[i];
            return null;
        }

        static Transform AppendPoint(KinematicMover mover, Vector3 position, Quaternion rotation)
        {
            var point = new GameObject($"Point {mover.points.Length + 1}").transform;
            Undo.RegisterCreatedObjectUndo(point.gameObject, "Add Point");
            point.SetParent(mover.transform, false);
            point.SetPositionAndRotation(position, rotation);
            Undo.RecordObject(mover, "Add Point");
            var points = new Transform[mover.points.Length + 1];
            System.Array.Copy(mover.points, points, mover.points.Length);
            points[points.Length - 1] = point;
            mover.points = points;
            mover.CachePath();
            return point;
        }

        // ------------------------------------------------------------ editing the selection

        public static void MakeMagnetic(GameObject go) => SetSurface(go, Layers.Magnetic, TestBedBuilder.EnsureMaterials().Magnetic, "Untagged");

        public static void MakePlain(GameObject go) => SetSurface(go, 0, TestBedBuilder.EnsureMaterials().Wall, "Untagged");

        public static void MakeLethal(GameObject go) => SetSurface(go, 0, TestBedBuilder.EnsureMaterials().Orange, Layers.LethalTag);

        static void SetSurface(GameObject go, int layer, Material material, string tag)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null || go.GetComponent<Collider>() == null)
                return;
            Undo.RecordObjects(new Object[] { go, renderer }, "Change Surface");
            go.layer = layer;
            go.tag = tag;
            renderer.sharedMaterial = material;
        }

        /// <summary>Turns the object 90° about a world axis through its own position.</summary>
        public static void Rotate90(GameObject go, Vector3 worldAxis)
        {
            Undo.RecordObject(go.transform, "Rotate 90°");
            go.transform.rotation = Quaternion.AngleAxis(90f, worldAxis) * go.transform.rotation;
        }

        /// <summary>
        /// Snaps to the grid. A box that isn't rotated off the 90° steps has its faces snapped
        /// (so odd sizes line up); anything else has its pivot snapped.
        /// </summary>
        public static void SnapToGrid(GameObject go)
        {
            Undo.RecordObject(go.transform, "Snap to Grid");
            Transform t = go.transform;
            var box = go.GetComponent<GreyboxBox>();
            if (box != null && IsAxisAligned(t.rotation))
            {
                Vector3 extent = Abs(t.rotation * box.size) * 0.5f;
                Vector3 min = SnapPoint(t.position - extent);
                t.position = min + extent;
            }
            else
            {
                t.position = SnapPoint(t.position);
            }
        }

        static bool IsAxisAligned(Quaternion rotation)
        {
            Vector3 e = rotation.eulerAngles;
            return Mathf.Abs(Mathf.DeltaAngle(e.x, Mathf.Round(e.x / 90f) * 90f)) < 0.5f
                && Mathf.Abs(Mathf.DeltaAngle(e.y, Mathf.Round(e.y / 90f) * 90f)) < 0.5f
                && Mathf.Abs(Mathf.DeltaAngle(e.z, Mathf.Round(e.z / 90f) * 90f)) < 0.5f;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
