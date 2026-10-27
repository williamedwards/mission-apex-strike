using UnityEditor;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>The Level Kit: buttons for building chambers. Open it from GravBoots → Level Kit.</summary>
    public class LevelKitWindow : EditorWindow
    {
        Vector2 scroll;
        bool showHelp = true;

        [MenuItem("GravBoots/Level Kit")]
        static void Open() => GetWindow<LevelKitWindow>("Level Kit");

        void OnEnable() => LevelKit.ApplySnapSettings();

        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            Header("Chamber");
            if (GUILayout.Button("New Chamber…"))
                EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("GravBoots/New Chamber…");
            var session = Object.FindAnyObjectByType<GravBoots.Level.LevelSession>();
            if (session == null && GUILayout.Button("Add Game Loop to This Chamber"))
                LevelKit.AddGameLoop();
            else if (session != null && session.GetComponent<GravBoots.Debugging.PlaytestLog>() == null
                     && GUILayout.Button("Add Playtest Log to This Chamber"))
                LevelKit.AddGameLoop();
            float step = EditorGUILayout.FloatField(new GUIContent("Grid step (m)", "Snapping for moving, resizing and new pieces."), LevelKit.GridStep);
            if (!Mathf.Approximately(step, LevelKit.GridStep))
                LevelKit.GridStep = step;

            Header("Add (at the Scene view's centre)");
            Row(("Plain Block", LevelKit.Piece.PlainBlock), ("Magnetic Block", LevelKit.Piece.MagneticBlock));
            Row(("Quarter-Pipe", LevelKit.Piece.QuarterPipe), ("Rounded Edge", LevelKit.Piece.RoundedEdge));
            Row(("Lethal Floor", LevelKit.Piece.LethalFloor), ("Kill Volume", LevelKit.Piece.KillVolume));
            Row(("Checkpoint", LevelKit.Piece.Checkpoint), ("Exit", LevelKit.Piece.Exit));
            Row(("Sentry", LevelKit.Piece.Sentry), ("Turret", LevelKit.Piece.Turret));
            if (GUILayout.Button("Spawn Point"))
                LevelKit.Place(LevelKit.Piece.Spawn);

            Header("Selected");
            GameObject[] selection = Selection.gameObjects;
            using (new EditorGUI.DisabledScope(selection.Length == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ForEach("Magnetic", selection, LevelKit.MakeMagnetic);
                    ForEach("Plain", selection, LevelKit.MakePlain);
                    ForEach("Lethal", selection, LevelKit.MakeLethal);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    ForEach("Turn 90° ↻ (Y)", selection, go => LevelKit.Rotate90(go, Vector3.up));
                    ForEach("Tilt 90° (X)", selection, go => LevelKit.Rotate90(go, Vector3.right));
                    ForEach("Roll 90° (Z)", selection, go => LevelKit.Rotate90(go, Vector3.forward));
                }
                ForEach("Snap to Grid", selection, LevelKit.SnapToGrid);
            }

            showHelp = EditorGUILayout.Foldout(showHelp, "Controls", true);
            if (showHelp)
            {
                EditorGUILayout.HelpBox(
                    "Scene view: hold right mouse + WASD to fly, scroll to zoom, F to frame the selection.\n" +
                    "Tools: W move, E rotate. Moving snaps to the grid step.\n" +
                    "Blocks: drag the small dots on each face to resize from that side.\n" +
                    "Curves: set Radius and Width in the Inspector; they start on a surface and curve toward its forward (blue arrow).\n" +
                    "Kill Volumes are invisible boxes (red outline) that kill on entry: fill the bottom of pits with them.\n" +
                    "Checkpoints and the Exit trigger when walked through; rotate a checkpoint onto a wall to respawn there magnetized.\n" +
                    "Sentries see and shoot only in their plane (red line round the room); drop onto one to destroy it.\n" +
                    "Turrets see the side their base faces (rotate onto any surface); walls break their line of sight.\n" +
                    "Ctrl+D duplicates, Ctrl+Z undoes. Ctrl+S saves the chamber. Press Play to test.\n" +
                    "Full guide: docs/LEVEL_EDITING.md",
                    MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        static void Header(string text)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        static void Row((string label, LevelKit.Piece piece) a, (string label, LevelKit.Piece piece) b)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(a.label))
                    LevelKit.Place(a.piece);
                if (GUILayout.Button(b.label))
                    LevelKit.Place(b.piece);
            }
        }

        static void ForEach(string label, GameObject[] selection, System.Action<GameObject> action)
        {
            if (!GUILayout.Button(label))
                return;
            foreach (GameObject go in selection)
                action(go);
        }
    }
}
