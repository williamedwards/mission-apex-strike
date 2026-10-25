using UnityEngine;

namespace GravBoots.Gravity
{
    /// <summary>
    /// Pure orientation math for walking on arbitrary surfaces. Kept free of MonoBehaviour
    /// state so it can be unit tested and shared by the player, enemies and portals.
    /// </summary>
    public static class SurfaceAlignment
    {
        /// <summary>
        /// Rotation whose up axis is <paramref name="surfaceNormal"/> while keeping the facing
        /// direction as close as possible to <paramref name="currentForward"/>.
        /// </summary>
        public static Quaternion AlignToSurface(Vector3 currentForward, Vector3 surfaceNormal)
        {
            Vector3 up = surfaceNormal.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(currentForward, up);

            // Facing straight into the new surface (e.g. walking into a wall): there is no
            // usable projection, so keep heading "up" the new surface instead.
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.up, up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);

            return Quaternion.LookRotation(forward.normalized, up);
        }

        /// <summary>
        /// Rotates <paramref name="current"/> so its up axis moves toward <paramref name="targetUp"/>
        /// by at most <paramref name="maxDegrees"/>, using the smallest rotation that does so.
        /// Used for surface transitions and for righting the player after the boots switch off.
        /// </summary>
        /// <remarks>
        /// When up and target are opposite (dropping off a ceiling), the shortest rotation is
        /// ambiguous and <see cref="Quaternion.RotateTowards"/> picks an arbitrary axis. Here we
        /// roll about the facing direction instead, so the player keeps their heading and the
        /// camera stays behind them.
        /// </remarks>
        public static Quaternion RotateUpToward(Quaternion current, Vector3 targetUp, float maxDegrees)
        {
            Vector3 up = current * Vector3.up;
            targetUp = targetUp.normalized;

            float angle = Vector3.Angle(up, targetUp);
            if (angle < 1e-3f)
                return current;

            Vector3 axis = Vector3.Cross(up, targetUp);
            if (axis.sqrMagnitude < 1e-4f)
                axis = current * Vector3.forward;

            return Quaternion.AngleAxis(Mathf.Min(angle, maxDegrees), axis.normalized) * current;
        }
    }
}
