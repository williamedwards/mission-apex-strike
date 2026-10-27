using GravBoots.Level;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// Scene view handles on a sized box (<see cref="ISizedBox"/>): drag any face dot to resize
    /// from that side, snapped to the Level Kit grid, while the opposite face stays where it is.
    /// </summary>
    [CustomEditor(typeof(GreyboxBox))]
    public class GreyboxBoxEditor : UnityEditor.Editor
    {
        protected virtual Color HandleColor => new Color(0.35f, 0.8f, 1f);

        readonly BoxBoundsHandle handle = new BoxBoundsHandle();
        // The handle works in the box's pose from when the drag started. Moving the box
        // mid-drag would otherwise shift the frame the handle measures the mouse in.
        Vector3 anchor;
        Quaternion anchorRotation;
        bool anchored;

        protected void OnSceneGUI()
        {
            var box = (ISizedBox)target;
            Transform t = ((Component)target).transform;
            if (!anchored || GUIUtility.hotControl == 0)
            {
                anchor = t.position;
                anchorRotation = t.rotation;
                anchored = true;
            }
            Vector3 current = Quaternion.Inverse(anchorRotation) * (t.position - anchor);

            using (new Handles.DrawingScope(HandleColor, Matrix4x4.TRS(anchor, anchorRotation, Vector3.one)))
            {
                handle.center = current;
                handle.size = box.Size;
                EditorGUI.BeginChangeCheck();
                handle.DrawHandle();
                if (!EditorGUI.EndChangeCheck())
                    return;

                Vector3 size = BoxResize.Snap(box.Size, handle.center - current, handle.size, LevelKit.GridStep, out Vector3 shift);
                if (size == box.Size)
                    return;
                Undo.RecordObjects(new Object[] { target, t }, "Resize Box");
                t.position = anchor + anchorRotation * (current + shift);
                box.Size = size;
                box.Rebuild();
            }
        }
    }

    /// <summary>The same face handles, in red, on kill volumes.</summary>
    [CustomEditor(typeof(KillVolume))]
    public class KillVolumeEditor : GreyboxBoxEditor
    {
        protected override Color HandleColor => new Color(1f, 0.3f, 0.2f);
    }
}
