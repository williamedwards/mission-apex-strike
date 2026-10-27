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
    }
}
