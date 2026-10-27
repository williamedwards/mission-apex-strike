using System.Collections.Generic;
using System.IO;
using System.Linq;
using GravBoots.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// Draws recorded playtests (<see cref="PlaytestLog"/>) for the open chamber in the Scene
    /// view: each run's path, red markers where the player died (labelled with the cause),
    /// and green dots where checkpoints were reached. Toggle under GravBoots → Playtests.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestViewer
    {
        const string ShowKey = "GravBoots.Playtests.Show";
        const string ShowMenu = "GravBoots/Playtests/Show in Scene View";

        static readonly List<PlaytestRecord> runs = new List<PlaytestRecord>();
        static string loadedScene;
        static double nextReload;

        static PlaytestViewer()
        {
            SceneView.duringSceneGui += Draw;
            EditorApplication.delayCall += () => Menu.SetChecked(ShowMenu, Show);
        }

        static bool Show
        {
            get => EditorPrefs.GetBool(ShowKey, false);
            set => EditorPrefs.SetBool(ShowKey, value);
        }

        [MenuItem(ShowMenu)]
        static void ToggleShow()
        {
            Show = !Show;
            Menu.SetChecked(ShowMenu, Show);
            nextReload = 0;
            SceneView.RepaintAll();
        }

        [MenuItem("GravBoots/Playtests/Open Folder")]
        static void OpenFolder()
        {
            Directory.CreateDirectory(PlaytestLog.Folder);
            EditorUtility.RevealInFinder(PlaytestLog.Folder);
        }

        /// <summary>Loads every recorded run of the named chamber, oldest first.</summary>
        public static List<PlaytestRecord> Load(string scene)
        {
            var result = new List<PlaytestRecord>();
            if (!Directory.Exists(PlaytestLog.Folder))
                return result;
            foreach (string file in Directory.GetFiles(PlaytestLog.Folder, scene + "_*.json").OrderBy(f => f))
            {
                try
                {
                    var record = JsonUtility.FromJson<PlaytestRecord>(File.ReadAllText(file));
                    if (record != null && record.scene == scene)
                        result.Add(record);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Playtest] Skipping {Path.GetFileName(file)}: {e.Message}");
                }
            }
            return result;
        }

        static void Draw(SceneView view)
        {
            if (!Show)
                return;
            string scene = EditorSceneManager.GetActiveScene().name;
            if (scene != loadedScene || EditorApplication.timeSinceStartup > nextReload)
            {
                runs.Clear();
                runs.AddRange(Load(scene));
                loadedScene = scene;
                nextReload = EditorApplication.timeSinceStartup + 3.0;
            }

            int deaths = 0, completed = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                PlaytestRecord run = runs[i];
                if (run.completed)
                    completed++;
                Color pathColor = Color.HSVToRGB(i * 0.17f % 1f, 0.5f, 1f);
                pathColor.a = 0.6f;
                Handles.color = pathColor;
                if (run.path.Count > 1)
                    Handles.DrawAAPolyLine(2.5f, run.path.ToArray());

                foreach (PlaytestEvent e in run.events)
                {
                    float size = HandleUtility.GetHandleSize(e.position) * 0.12f;
                    if (e.type == "death")
                    {
                        deaths++;
                        Handles.color = new Color(1f, 0.15f, 0.1f, 0.9f);
                        Handles.SphereHandleCap(0, e.position + e.up * 0.9f, Quaternion.identity, size * 1.5f, EventType.Repaint);
                        Handles.Label(e.position + e.up * 1.4f, e.detail, EditorStyles.whiteMiniLabel);
                    }
                    else if (e.type == "checkpoint")
                    {
                        Handles.color = new Color(0.3f, 1f, 0.6f, 0.9f);
                        Handles.SphereHandleCap(0, e.position + e.up * 0.2f, Quaternion.identity, size, EventType.Repaint);
                    }
                }
            }

            Handles.BeginGUI();
            GUI.Label(new Rect(10f, 10f, 420f, 20f),
                $"Playtests of {scene}: {runs.Count} runs, {completed} completed, {deaths} deaths",
                EditorStyles.helpBox);
            Handles.EndGUI();
        }
    }
}
