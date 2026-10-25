using UnityEngine;

namespace GravBoots.Cameras
{
    /// <summary>Tuning values for <see cref="CameraRig"/>; editable while playing.</summary>
    [CreateAssetMenu(menuName = "GravBoots/Camera Settings", fileName = "CameraSettings")]
    public class CameraSettings : ScriptableObject
    {
        [Header("Rig")]
        [Tooltip("Height of the orbit pivot above the player's feet, along the player's up.")]
        public float pivotHeight = 1.45f;
        public Vector3 shoulderOffset = new Vector3(0.55f, 0.15f, 0f);
        public float verticalArmLength = 0.2f;
        public float cameraDistance = 3.5f;
        public Vector3 damping = new Vector3(0.1f, 0.25f, 0.3f);
        public float fieldOfView = 65f;

        [Header("Obstacles")]
        [Tooltip("On: CameraRig's smoothed avoidance. Off: Cinemachine's built-in avoidance, which snaps in instantly (kept for comparison).")]
        public bool smoothObstacleAvoidance = true;
        [Tooltip("Radius of the sphere the camera keeps clear of geometry.")]
        public float collisionRadius = 0.2f;
        [Tooltip("Extra clearance for the shoulder probe, so the shoulder tucks in before it touches a wall.")]
        public float shoulderMargin = 0.25f;
        [Tooltip("Fastest the shoulder offset retracts, as a fraction of its length per second.")]
        public float shoulderRetractPerSecond = 4f;
        [Tooltip("Fastest the camera pulls in toward the player (m/s). Continuous approaches slower than this are tracked exactly; sudden obstructions are smoothed.")]
        public float pullInSpeed = 12f;
        [Tooltip("Time constant for easing the shoulder and camera back out once clear (seconds).")]
        public float easeOutTime = 0.35f;

        [Header("Look")]
        public float mouseDegreesPerPixel = 0.1f;
        public float stickDegreesPerSecond = 200f;
        public bool invertY;
        public float minPitch = -70f;
        public float maxPitch = 80f;

        [Header("Following the player's up")]
        [Tooltip("Exponential catch-up rate for the camera's up (higher is snappier).")]
        public float upSharpness = 8f;
        [Tooltip("The camera's up always turns at least this fast while catching up.")]
        public float minUpDegreesPerSecond = 60f;
        [Tooltip("Partial Follow: the most the camera rolls away from world up.")]
        public float partialMaxRollDegrees = 90f;
        [Tooltip("Recenter On Settle: how long the player's up must stay put before the camera follows.")]
        public float settleTime = 0.3f;
    }
}
