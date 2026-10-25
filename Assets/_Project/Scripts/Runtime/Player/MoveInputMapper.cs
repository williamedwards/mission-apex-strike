using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// Turns stick input into a world-space move direction on the player's surface plane.
    /// </summary>
    /// <remarks>
    /// Normally the mapping is camera-relative. While the player's up is changing faster than
    /// the camera can follow (walking into a wall, being righted after a drop), a held stick
    /// keeps the mapping it started with, carried around by the change in up. Holding forward
    /// into a wall therefore keeps meaning "forward", which is now "up the wall". The hold
    /// ends when the stick is released or turned more than <see cref="HoldBreakAngle"/>.
    /// See docs/PLAN.md §1.4.
    /// </remarks>
    public class MoveInputMapper
    {
        /// <summary>Camera/player up difference (degrees) above which the player counts as transitioning.</summary>
        public float TransitionAngle = 10f;
        /// <summary>Stick rotation (degrees) that ends a held mapping.</summary>
        public float HoldBreakAngle = 45f;

        bool holding;
        Vector2 holdStick;
        Vector3 basisForward;
        Vector3 basisUp;

        public bool IsHoldingMapping { get; private set; }

        public Vector3 Map(Vector2 stick, Vector3 cameraForward, Vector3 cameraUp, Vector3 playerUp)
        {
            playerUp = playerUp.normalized;
            float magnitude = Mathf.Min(1f, stick.magnitude);
            if (magnitude < 1e-3f)
            {
                holding = false;
                IsHoldingMapping = false;
                return Vector3.zero;
            }

            bool transitioning = Vector3.Angle(cameraUp, playerUp) > TransitionAngle;
            bool newHold = !holding || Vector2.Angle(holdStick, stick) > HoldBreakAngle;

            if (newHold || !transitioning)
            {
                basisForward = CameraForwardOnPlane(cameraForward, cameraUp, playerUp);
                if (newHold)
                    holdStick = stick;
                IsHoldingMapping = false;
            }
            else
            {
                basisForward = Vector3.ProjectOnPlane(Quaternion.FromToRotation(basisUp, playerUp) * basisForward, playerUp);
                if (basisForward.sqrMagnitude < 1e-6f)
                    basisForward = CameraForwardOnPlane(cameraForward, cameraUp, playerUp);
                basisForward.Normalize();
                IsHoldingMapping = true;
            }

            holding = true;
            basisUp = playerUp;

            Vector3 right = Vector3.Cross(playerUp, basisForward);
            Vector3 direction = basisForward * stick.y + right * stick.x;
            return direction.normalized * magnitude;
        }

        static Vector3 CameraForwardOnPlane(Vector3 cameraForward, Vector3 cameraUp, Vector3 playerUp)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, playerUp);
            // Looking straight along the player's up (e.g. down from a ceiling): use the camera's
            // up as "forward" instead, which is where the top of the screen points.
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(cameraUp, playerUp);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, playerUp);
            return forward.normalized;
        }
    }
}
