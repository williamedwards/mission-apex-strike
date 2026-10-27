using UnityEngine;

namespace GravBoots.Level
{
    public enum MoverMode
    {
        /// <summary>Out to the last point and back, pausing at each end.</summary>
        PingPong,
        /// <summary>Round the points and back to the first, pausing there.</summary>
        Loop,
        /// <summary>Waits at the first point until the player stands on it, then travels to the last once.</summary>
        OnStep,
    }

    /// <summary>
    /// Pure path maths for <see cref="KinematicMover"/>: how far along its path a mover is at a
    /// given time, and the pose at a distance along the path.
    /// </summary>
    public static class MoverPath
    {
        /// <summary>
        /// Cumulative length at each point (0 at the first); a closed path adds the way back to
        /// the first. Turning counts too, as the arc a point 1 m from the pivot travels, so a
        /// turntable whose points only rotate still has a length to travel at its speed.
        /// </summary>
        public static float[] Cumulative(Vector3[] positions, Quaternion[] rotations, bool closed)
        {
            int n = positions.Length;
            int count = n + (closed && n > 1 ? 1 : 0);
            var cumulative = new float[Mathf.Max(1, count)];
            for (int i = 1; i < count; i++)
            {
                int a = i - 1, b = i % n;
                float turn = Quaternion.Angle(rotations[a], rotations[b]) * Mathf.Deg2Rad;
                cumulative[i] = cumulative[i - 1] + Mathf.Max(Vector3.Distance(positions[a], positions[b]), turn);
            }
            return cumulative;
        }

        /// <summary>
        /// Distance along a path of <paramref name="length"/> at <paramref name="time"/>.
        /// Ping-pong and on-step trips ease in and out (so riders aren't jolted), averaging
        /// <paramref name="speed"/>; loops run at constant speed.
        /// </summary>
        public static float Distance(float time, float length, float speed, float wait, MoverMode mode)
        {
            if (length <= 0f || speed <= 0f)
                return 0f;
            float travel = length / speed;
            wait = Mathf.Max(0f, wait);
            switch (mode)
            {
                case MoverMode.Loop:
                {
                    float u = Mathf.Repeat(time, travel + wait);
                    return u < wait ? 0f : (u - wait) * speed;
                }
                case MoverMode.OnStep:
                    return time <= 0f ? 0f : length * Ease(time / travel);
                default:
                {
                    float u = Mathf.Repeat(time, 2f * (travel + wait));
                    if (u < wait)
                        return 0f;
                    u -= wait;
                    if (u < travel)
                        return length * Ease(u / travel);
                    u -= travel;
                    if (u < wait)
                        return length;
                    u -= wait;
                    return length * (1f - Ease(u / travel));
                }
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The pose <paramref name="distance"/> along the path (rotations slerped along each segment).</summary>
        public static void Sample(Vector3[] positions, Quaternion[] rotations, float[] cumulative, float distance,
            out Vector3 position, out Quaternion rotation)
        {
            int n = positions.Length;
            if (n == 1 || cumulative.Length < 2)
            {
                position = positions[0];
                rotation = rotations[0];
                return;
            }
            distance = Mathf.Clamp(distance, 0f, cumulative[cumulative.Length - 1]);
            int segment = 0;
            while (segment < cumulative.Length - 2 && distance > cumulative[segment + 1])
                segment++;
            float span = cumulative[segment + 1] - cumulative[segment];
            float t = span > 1e-6f ? (distance - cumulative[segment]) / span : 0f;
            int a = segment % n, b = (segment + 1) % n;
            position = Vector3.Lerp(positions[a], positions[b], t);
            rotation = Quaternion.Slerp(rotations[a], rotations[b], t);
        }
    }
}
