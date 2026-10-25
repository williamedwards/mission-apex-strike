using UnityEngine;

namespace GravBoots.Gravity
{
    /// <summary>
    /// The gravity frame of one body: which way is up for it. Gravity pulls along -<see cref="Up"/>.
    /// </summary>
    /// <remarks>
    /// Changing Up can be instant (dropping: world gravity applies on the very next step) or
    /// blended at a fixed angular speed (debug tool, later curved surfaces). The body's
    /// <i>rotation</i> is a separate concern owned by the motor, which turns to follow Up.
    /// </remarks>
    public class GravityBody : MonoBehaviour
    {
        [SerializeField] Vector3 initialUp = Vector3.up;

        Vector3 up;
        Vector3 targetUp;
        float blendDegreesPerSecond;

        public Vector3 Up => up;
        public Vector3 TargetUp => targetUp;
        public bool IsBlending => up != targetUp;

        void Awake()
        {
            up = targetUp = initialUp.sqrMagnitude > 0f ? initialUp.normalized : Vector3.up;
        }

        /// <summary>Sets Up immediately.</summary>
        public void SetUp(Vector3 newUp)
        {
            up = targetUp = newUp.normalized;
        }

        /// <summary>Rotates Up toward <paramref name="newUp"/> at a fixed angular speed.</summary>
        public void BlendUp(Vector3 newUp, float degreesPerSecond)
        {
            targetUp = newUp.normalized;
            blendDegreesPerSecond = degreesPerSecond;
        }

        /// <summary>
        /// Advances a blend. <paramref name="referenceForward"/> picks the rotation axis when
        /// the target is directly opposite (a roll about the facing direction).
        /// </summary>
        public void Tick(float deltaTime, Vector3 referenceForward)
        {
            if (!IsBlending)
                return;

            Quaternion frame = SurfaceAlignment.AlignToSurface(referenceForward, up);
            frame = SurfaceAlignment.RotateUpToward(frame, targetUp, blendDegreesPerSecond * deltaTime);
            up = frame * Vector3.up;
            if (Vector3.Angle(up, targetUp) < 0.01f)
                up = targetUp;
        }
    }
}
