using UnityEngine;

namespace GravBoots.UI
{
    /// <summary>
    /// A slow camera move through one room for the title and end screens, in the room's own
    /// frame (entry door at the origin, floor at y = 0, running along +z): from high up by the
    /// entry wall, drifting in and across while looking down the room.
    /// </summary>
    public struct PanoramaShot
    {
        public Vector3 From, To, LookFrom, LookTo;

        /// <summary>Clearance kept from the walls, ceiling and entry wall (m).</summary>
        public const float Margin = 0.8f;

        /// <summary>
        /// The shot for a room whose inside runs from <paramref name="west"/> to
        /// <paramref name="east"/> across, <paramref name="length"/> deep and
        /// <paramref name="height"/> high. <paramref name="side"/> (±1) picks which side it
        /// starts on, so consecutive shots alternate.
        /// </summary>
        public static PanoramaShot ForRoom(float west, float east, float length, float height, int side)
        {
            float mid = (west + east) * 0.5f, half = (east - west) * 0.5f;
            float s = side >= 0 ? 1f : -1f;
            float y = Mathf.Clamp(height * 0.62f, 1f, height - Margin);
            float x = Mathf.Clamp(mid + s * half * 0.45f, west + Margin, east - Margin);
            float z = Mathf.Min(Margin + 0.2f, length * 0.5f);
            var from = new Vector3(x, y, z);
            var to = new Vector3(Mathf.Clamp(mid + s * half * 0.15f, west + Margin, east - Margin),
                Mathf.Clamp(height * 0.55f, 1f, height - Margin), Mathf.Clamp(length * 0.3f, z, length - Margin));
            return new PanoramaShot
            {
                From = from,
                To = to,
                LookFrom = new Vector3(mid - s * half * 0.2f, height * 0.15f, length * 0.85f),
                LookTo = new Vector3(mid, height * 0.2f, length),
            };
        }

        /// <summary>Camera position and rotation (room frame) at <paramref name="t"/>, 0 → 1, eased.</summary>
        public void Sample(float t, out Vector3 position, out Quaternion rotation)
        {
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            position = Vector3.Lerp(From, To, e);
            Vector3 look = Vector3.Lerp(LookFrom, LookTo, e) - position;
            rotation = Quaternion.LookRotation(look.sqrMagnitude > 1e-6f ? look : Vector3.forward, Vector3.up);
        }
    }
}
