using UnityEngine;

namespace GravBoots.Hazards
{
    /// <summary>Pure geometry for beams: whether a straight beam touches a capsule.</summary>
    public static class BeamMath
    {
        /// <summary>The shortest distance between segments <paramref name="p0"/>–<paramref name="p1"/> and <paramref name="q0"/>–<paramref name="q1"/>.</summary>
        public static float SegmentDistance(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1)
        {
            Vector3 d1 = p1 - p0, d2 = q1 - q0, r = p0 - q0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            const float eps = 1e-8f;
            if (a <= eps && e <= eps)
                return r.magnitude;
            if (a <= eps)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > eps ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }
            return Vector3.Distance(p0 + d1 * s, q0 + d2 * t);
        }

        /// <summary>
        /// Whether a beam from <paramref name="from"/> to <paramref name="to"/>, of radius
        /// <paramref name="beamRadius"/>, touches a capsule standing on <paramref name="foot"/>
        /// along <paramref name="up"/> (any orientation).
        /// </summary>
        public static bool HitsCapsule(Vector3 from, Vector3 to, float beamRadius, Vector3 foot, Vector3 up, float height, float radius)
        {
            Vector3 bottom = foot + up * radius;
            Vector3 top = foot + up * Mathf.Max(radius, height - radius);
            return SegmentDistance(from, to, bottom, top) <= radius + beamRadius;
        }

        /// <summary>
        /// Whether a capsule standing on <paramref name="foot"/> along <paramref name="up"/>
        /// reaches into a flat slab (the plane through <paramref name="planePoint"/> with normal
        /// <paramref name="planeNormal"/>, <paramref name="halfThickness"/> either side).
        /// <paramref name="aimPoint"/> is where to aim, in the plane, to hit the capsule.
        /// </summary>
        public static bool CapsuleInSlab(Vector3 foot, Vector3 up, float height, float radius,
            Vector3 planePoint, Vector3 planeNormal, float halfThickness, out Vector3 aimPoint) =>
            CapsuleInSlab(foot, up, height, radius, planePoint, planeNormal, halfThickness, out aimPoint, out _);

        /// <inheritdoc cref="CapsuleInSlab(Vector3, Vector3, float, float, Vector3, Vector3, float, out Vector3)"/>
        /// <param name="bodyPoint">The point on the capsule's axis nearest the plane (for line of sight to the body itself).</param>
        public static bool CapsuleInSlab(Vector3 foot, Vector3 up, float height, float radius,
            Vector3 planePoint, Vector3 planeNormal, float halfThickness, out Vector3 aimPoint, out Vector3 bodyPoint)
        {
            Vector3 a = foot + up * radius;
            Vector3 b = foot + up * Mathf.Max(radius, height - radius);
            float sa = Vector3.Dot(a - planePoint, planeNormal);
            float sb = Vector3.Dot(b - planePoint, planeNormal);
            Vector3 nearest;
            float distance;
            if (sa * sb <= 0f && !Mathf.Approximately(sa, sb))
            {
                // The body's axis crosses the plane.
                nearest = Vector3.Lerp(a, b, sa / (sa - sb));
                distance = 0f;
            }
            else if (Mathf.Abs(sa - sb) < 1e-4f)
            {
                // Lying along the plane (e.g. standing on a wall beside it).
                nearest = (a + b) * 0.5f;
                distance = Mathf.Abs(sa);
            }
            else
            {
                bool aCloser = Mathf.Abs(sa) < Mathf.Abs(sb);
                nearest = aCloser ? a : b;
                distance = Mathf.Abs(aCloser ? sa : sb);
            }
            bodyPoint = nearest;
            aimPoint = nearest - planeNormal * Vector3.Dot(nearest - planePoint, planeNormal);
            return distance <= radius + halfThickness;
        }
    }
}
