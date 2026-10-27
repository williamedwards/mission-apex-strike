using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>Snapping maths for resizing a box by dragging one face, keeping the opposite face put.</summary>
    public static class BoxResize
    {
        /// <summary>
        /// Takes the result of dragging a face with a bounds handle (centred on the old box) and
        /// snaps it: each changed axis gets a size that's a multiple of <paramref name="step"/>
        /// (at least one step), and the centre moves so the face that wasn't dragged stays put.
        /// </summary>
        /// <param name="oldSize">Box size before the drag.</param>
        /// <param name="handleCenter">The handle's new centre, relative to the old centre.</param>
        /// <param name="handleSize">The handle's new size.</param>
        /// <param name="centerShift">How far to move the box's centre, in its local axes.</param>
        public static Vector3 Snap(Vector3 oldSize, Vector3 handleCenter, Vector3 handleSize, float step, out Vector3 centerShift)
        {
            Vector3 size = oldSize;
            centerShift = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float change = handleSize[axis] - oldSize[axis];
                if (Mathf.Abs(change) < 1e-5f)
                    continue;
                float snapped = Mathf.Max(step, Mathf.Round(handleSize[axis] / step) * step);
                // Which face moved: the centre shifts toward a face that grew and away from one that shrank.
                float face = Mathf.Sign(handleCenter[axis]) * Mathf.Sign(change);
                size[axis] = snapped;
                centerShift[axis] = face * (snapped - oldSize[axis]) * 0.5f;
            }
            return size;
        }
    }
}
