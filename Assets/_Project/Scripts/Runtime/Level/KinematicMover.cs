using GravBoots.Player;
using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// Moves <see cref="platform"/> along a path of <see cref="points"/> (each a position and a
    /// rotation), driven by the <see cref="LevelClock"/>. Anything parented to the platform rides
    /// along (a sentry on a track); the player rides it by standing on it.
    /// </summary>
    /// <remarks>
    /// Keep the points as children of this object, not of the platform, so the whole thing
    /// moves together in the editor. Rotating a point turns the platform as it passes, which is
    /// how a track follows a curve onto a wall. In the editor the platform sits at its pose at
    /// time 0.
    /// </remarks>
    [ExecuteAlways]
    public class KinematicMover : MonoBehaviour
    {
        public Transform platform;
        public Transform[] points = new Transform[0];
        public MoverMode mode = MoverMode.PingPong;
        [Tooltip("Average speed along the path (m/s). Turning counts as the arc 1 m from the pivot.")]
        [Min(0.01f)] public float speed = 2f;
        [Tooltip("Pause at each end (ping-pong) or at the first point (loop), in seconds.")]
        [Min(0f)] public float wait = 1f;
        [Tooltip("Seconds ahead of the clock, to stagger movers that share a path shape.")]
        public float phaseOffset;

        Vector3[] positions;
        Quaternion[] rotations;
        float[] cumulative;
        float triggerTime = -1f;
        PlayerMotor rider;

        public float Length => cumulative != null && cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f;
        public bool Triggered => triggerTime >= 0f;

        void OnEnable()
        {
            CachePath();
            if (Application.isPlaying)
                LevelClock.Ensure().Register(this);
            else
                Evaluate(0f);
        }

        void OnDisable()
        {
            if (Application.isPlaying && LevelClock.Instance != null)
                LevelClock.Instance.Unregister(this);
        }

        void Update()
        {
            // Editing: follow the points as they're moved.
            if (!Application.isPlaying)
            {
                CachePath();
                Evaluate(0f);
            }
        }

        /// <summary>Reads the points' poses (they're static during play).</summary>
        public void CachePath()
        {
            int n = 0;
            foreach (Transform p in points)
                if (p != null)
                    n++;
            positions = new Vector3[n];
            rotations = new Quaternion[n];
            int i = 0;
            foreach (Transform p in points)
            {
                if (p == null)
                    continue;
                positions[i] = p.position;
                rotations[i] = p.rotation;
                i++;
            }
            cumulative = n > 0 ? MoverPath.Cumulative(positions, rotations, mode == MoverMode.Loop) : null;
        }

        /// <summary>Puts the platform where it is at clock <paramref name="time"/>.</summary>
        public void Evaluate(float time)
        {
            if (platform == null || positions == null || positions.Length == 0)
                return;
            float local = time + phaseOffset;
            if (mode == MoverMode.OnStep)
            {
                if (!Triggered && Application.isPlaying && RiderOnPlatform())
                    triggerTime = time;
                local = Triggered ? time - triggerTime : 0f;
            }
            float distance = MoverPath.Distance(local, Length, speed, wait, mode);
            MoverPath.Sample(positions, rotations, cumulative, distance, out Vector3 position, out Quaternion rotation);
            // Only when it changes, so the editor doesn't keep marking the scene modified.
            if (platform.position != position || platform.rotation != rotation)
                platform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>The clock went back to <paramref name="time"/>: forget a trigger that came later.</summary>
        public void Rewind(float time)
        {
            if (triggerTime > time)
                triggerTime = -1f;
        }

        bool RiderOnPlatform()
        {
            if (rider == null)
                rider = FindAnyObjectByType<PlayerMotor>();
            return rider != null && rider.IsGrounded && rider.GroundCollider != null
                && rider.GroundCollider.transform.IsChildOf(platform);
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length == 0)
                return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Transform previous = null;
            foreach (Transform p in points)
            {
                if (p == null)
                    continue;
                Gizmos.DrawWireSphere(p.position, 0.2f);
                Gizmos.DrawRay(p.position, p.up * 0.6f);
                if (previous != null)
                    Gizmos.DrawLine(previous.position, p.position);
                previous = p;
            }
            if (mode == MoverMode.Loop && previous != null && points[0] != null)
                Gizmos.DrawLine(previous.position, points[0].position);
        }
    }
}
