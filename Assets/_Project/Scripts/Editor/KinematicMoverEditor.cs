using GravBoots.Level;
using UnityEditor;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>Path buttons for a <see cref="KinematicMover"/>: add points straight on, or round a curve.</summary>
    [CustomEditor(typeof(KinematicMover))]
    public class KinematicMoverEditor : UnityEditor.Editor
    {
        float arcRadius = TestBedBuilder.QuarterPipeRadius;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var mover = (KinematicMover)target;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{mover.points.Length} points, {mover.Length:0.0} m");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point"))
                    Select(LevelKit.AddPoint(mover));
                if (GUILayout.Button("Remove Last Point"))
                    LevelKit.RemoveLastPoint(mover);
            }
            arcRadius = EditorGUILayout.FloatField(new GUIContent("Arc radius (m)", "Match the curve the track follows."), arcRadius);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Curve Arc (up a wall)"))
                    LevelKit.AddArc(mover, arcRadius, true);
                if (GUILayout.Button("Add Edge Arc (over an edge)"))
                    LevelKit.AddArc(mover, arcRadius, false);
            }
            EditorGUILayout.HelpBox(
                "Points are this object's children: move or rotate them to shape the path (the short line at each point is the platform's up there). " +
                "The platform shows where it is at the start; press Play to see it move.", MessageType.None);
        }

        static void Select(Transform point)
        {
            if (point != null)
                Selection.activeTransform = point;
        }
    }
}
